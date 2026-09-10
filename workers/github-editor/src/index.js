const json = (body, init = {}) => new Response(JSON.stringify(body), {
  ...init,
  headers: { "content-type": "application/json; charset=utf-8", ...(init.headers || {}) }
});
function originAllowed(request, env) { const origin = request.headers.get("origin"); return !origin || origin === env.PAGES_ORIGIN; }
function cors(request, env, response) { const headers = new Headers(response.headers); headers.set("Access-Control-Allow-Origin", env.PAGES_ORIGIN); headers.set("Access-Control-Allow-Credentials", "true"); headers.set("Vary", "Origin"); return new Response(response.body, { status: response.status, headers }); }
function cookie(request, name) { const value = request.headers.get("cookie") || ""; return value.split(/;\s*/).map(part => part.split("=", 2)).find(([key]) => key === name)?.[1] || ""; }
function random() { return crypto.randomUUID().replaceAll("-", ""); }
async function github(url, token, init = {}) { const response = await fetch(`https://api.github.com${url}`, { ...init, headers: { Accept: "application/vnd.github+json", Authorization: `Bearer ${token}`, "X-GitHub-Api-Version": "2022-11-28", ...(init.headers || {}) } }); if (!response.ok) throw new Error(`GitHub API ${response.status}`); return response; }
async function session(request, env) { const bearer = request.headers.get("authorization") || ""; const id = cookie(request, "qimiao_editor_session") || (bearer.startsWith("Bearer ") ? bearer.slice(7).trim() : ""); return id ? await env.SESSIONS.get(`session:${id}`) : null; }
function validateQueue(queue) { if (!Array.isArray(queue)) throw new Error("queue 必须是数组。"); const seen = new Set(); return queue.map((item, index) => { const platform = String(item?.platform || "").trim().toUpperCase(), artworkId = String(item?.artworkId || "").trim(); if (!platform || !artworkId || !/^[\w.-]+$/.test(artworkId)) throw new Error(`第 ${index + 1} 项图片标识无效。`); const key = `${platform}\u001f${artworkId}`; if (seen.has(key)) throw new Error("确认区不能有重复图片。"); seen.add(key); return { platform, artworkId, queueOrder: index + 1 }; }); }
const editableDataFiles = new Set(["activities.json", "banners.json", "versions.json", "birthdays.json", "anniversaries.json", "calendar-events.json", "endgame-rules.json", "endgame-overrides.json"]);
function validateManualRecords(file, records) {
  if (!editableDataFiles.has(file)) throw new Error("不允许编辑该文件。");
  if (!Array.isArray(records)) throw new Error("数据必须是数组。");
  if (records.length > 2000) throw new Error("数据量超过限制。");
  const ids = new Set();
  for (const [index, record] of records.entries()) {
    if (!record || typeof record !== "object" || Array.isArray(record)) throw new Error(`第 ${index + 1} 项必须是对象。`);
    const id = String(record.id || record.ruleId || "").trim();
    if (!id || ids.has(id)) throw new Error(`第 ${index + 1} 项缺少唯一标识。`);
    ids.add(id);
  }
  return records;
}
async function writeDataFile(file, records, token, env) {
  const path = `data/${file}`;
  const existing = await (await github(`/repos/${env.REPOSITORY}/contents/${path}?ref=${encodeURIComponent(env.BRANCH)}`, token)).json();
  const content = btoa(unescape(encodeURIComponent(JSON.stringify(records, null, 2) + "\n")));
  return (await github(`/repos/${env.REPOSITORY}/contents/${path}`, token, { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify({ message: `chore(data): update ${file}`, content, sha: existing.sha, branch: env.BRANCH }) })).json();
}

export default { async fetch(request, env) {
  const url = new URL(request.url);
  if (request.method === "OPTIONS") return cors(request, env, new Response(null, { headers: { "Access-Control-Allow-Methods": "GET,POST,PUT,OPTIONS", "Access-Control-Allow-Headers": "content-type,authorization" } }));
  if (!originAllowed(request, env)) return new Response("Forbidden origin", { status: 403 });
  try {
    if (url.pathname === "/api/session") return cors(request, env, json({ authenticated: Boolean(await session(request, env)) }));
    if (url.pathname === "/api/login") { const state = random(); await env.SESSIONS.put(`state:${state}`, url.searchParams.get("returnTo") || env.PAGES_ORIGIN, { expirationTtl: 600 }); const login = new URL("https://github.com/login/oauth/authorize"); login.search = new URLSearchParams({ client_id: env.GITHUB_OAUTH_CLIENT_ID, redirect_uri: `${url.origin}/api/callback`, scope: "repo", state }); return new Response(null, { status: 302, headers: { Location: login, "Set-Cookie": `qimiao_oauth_state=${state}; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=600` } }); }
    if (url.pathname === "/api/callback") { const state = url.searchParams.get("state") || ""; if (!state || state !== cookie(request, "qimiao_oauth_state")) return new Response("Invalid OAuth state", { status: 400 }); const returnTo = await env.SESSIONS.get(`state:${state}`); if (!returnTo) return new Response("Expired OAuth state", { status: 400 }); const response = await fetch("https://github.com/login/oauth/access_token", { method: "POST", headers: { Accept: "application/json", "content-type": "application/json" }, body: JSON.stringify({ client_id: env.GITHUB_OAUTH_CLIENT_ID, client_secret: env.GITHUB_OAUTH_CLIENT_SECRET, code: url.searchParams.get("code") }) }); const token = await response.json(); if (!token.access_token) return new Response("GitHub authorization failed", { status: 401 }); const id = random(); await env.SESSIONS.put(`session:${id}`, token.access_token, { expirationTtl: 3600 }); const pageOrigin = new URL(env.PAGES_ORIGIN).origin; const body = `<!doctype html><meta charset="utf-8"><title>绮喵日报授权完成</title><p>授权完成，正在返回原页面…</p><script>window.opener&&window.opener.postMessage({type:"qimiao-editor-auth",session:"${id}"},"${pageOrigin}");window.close();</script>`; return new Response(body, { headers: { "content-type": "text/html; charset=utf-8", "Set-Cookie": `qimiao_editor_session=${id}; HttpOnly; Secure; SameSite=None; Path=/; Max-Age=3600` } }); }
    if (url.pathname === "/api/artwork-queue" && request.method === "PUT") { const token = await session(request, env); if (!token) return cors(request, env, json({ error: "请先登录 GitHub。" }, { status: 401 })); const queue = validateQueue((await request.json()).queue); const path = "data/artwork-queue.json"; const existing = await (await github(`/repos/${env.REPOSITORY}/contents/${path}?ref=${encodeURIComponent(env.BRANCH)}`, token)).json(); const content = btoa(unescape(encodeURIComponent(JSON.stringify(queue, null, 2) + "\n"))); const saved = await (await github(`/repos/${env.REPOSITORY}/contents/${path}`, token, { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify({ message: `chore(artwork): update confirmed queue (${queue.length})`, content, sha: existing.sha, branch: env.BRANCH }) })).json(); return cors(request, env, json({ ok: true, commitUrl: saved.commit?.html_url || null })); }
    if (url.pathname === "/api/collect-artwork" && request.method === "POST") { const token = await session(request, env); if (!token) return cors(request, env, json({ error: "请先登录 GitHub。" }, { status: 401 })); const requestedAt = new Date().toISOString(); try { await github(`/repos/${env.REPOSITORY}/actions/workflows/collect.yml/dispatches`, token, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ ref: env.BRANCH, inputs: { replace_artwork_candidates: "true" } }) }); } catch (error) { if (String(error?.message || error).includes("403")) return cors(request, env, json({ error: "需要重新授权 GitHub 的仓库与 Actions 权限。" }, { status: 403 })); throw error; } return cors(request, env, json({ ok: true, requestedAt })); }
    if (url.pathname === "/api/collect-artwork-status" && request.method === "GET") { const token = await session(request, env); if (!token) return cors(request, env, json({ error: "请先登录 GitHub。" }, { status: 401 })); const after = Date.parse(url.searchParams.get("after") || "") || 0; const runs = await (await github(`/repos/${env.REPOSITORY}/actions/workflows/collect.yml/runs?event=workflow_dispatch&per_page=10`, token)).json(); const run = (runs.workflow_runs || []).filter(item => Date.parse(item.created_at || "") >= after - 5000).sort((a, b) => Date.parse(b.created_at) - Date.parse(a.created_at))[0]; if (!run) return cors(request, env, json({ phase: "WAITING" })); return cors(request, env, json({ phase: run.status === "completed" ? (run.conclusion === "success" ? "COMPLETE" : "FAILED") : run.status === "in_progress" ? "COLLECTING" : "QUEUED", conclusion: run.conclusion || null, runUrl: run.html_url || null })); }
    const dataMatch = url.pathname.match(/^\/api\/data\/([a-z-]+\.json)$/);
    if (dataMatch && request.method === "PUT") { const token = await session(request, env); if (!token) return cors(request, env, json({ error: "请先登录 GitHub。" }, { status: 401 })); const file = dataMatch[1]; const records = validateManualRecords(file, (await request.json()).records); const saved = await writeDataFile(file, records, token, env); return cors(request, env, json({ ok: true, commitUrl: saved.commit?.html_url || null })); }
    return cors(request, env, json({ error: "Not found" }, { status: 404 }));
  } catch (error) { return cors(request, env, json({ error: error instanceof Error ? error.message : "服务异常" }, { status: 500 })); }
} };
