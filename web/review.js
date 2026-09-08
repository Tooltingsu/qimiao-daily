const state = { items: [], queue: [], config: null };
const $ = selector => document.querySelector(selector);
const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"})[c]);
const keyOf = item => `${item.platform}\u001f${item.artworkId}`;
const gameName = value => ({ GENSHIN: "原神", STARRAIL: "崩坏：星穹铁道", NTE: "异环", HI3: "崩坏3", ZZZ: "绝区零", WUWA: "鸣潮" })[value] ?? value;

function queuePayload() { return state.queue.map((item, index) => ({ platform: item.platform, artworkId: item.artworkId, queueOrder: index + 1 })); }
function showNotice(message, kind = "") { const node=$("#editor-notice"); node.textContent=message; node.className=`editor-notice ${kind}`; }
function imageMarkup(item) {
  return item.thumbnailUrl
    ? `<img loading="lazy" src="${escapeHtml(item.thumbnailUrl)}" alt="${escapeHtml(item.character)}" onerror="this.replaceWith(document.createTextNode('缩略图不可用'))">`
    : `<div class="art-placeholder">无公开缩略图</div>`;
}
function renderQueue() {
  $("#queue-count").textContent=String(state.queue.length);
  $("#queue-list").innerHTML = state.queue.length ? state.queue.map((item,index) => `<li class="queue-item">
    <span class="queue-order">${index + 1}</span><span class="queue-name">${escapeHtml(item.character)}<small>${escapeHtml(gameName(item.franchise))}</small></span>
    <span class="queue-buttons"><button data-move="up" data-index="${index}" aria-label="上移 ${escapeHtml(item.character)}" ${index===0?"disabled":""}>↑</button><button data-move="down" data-index="${index}" aria-label="下移 ${escapeHtml(item.character)}" ${index===state.queue.length-1?"disabled":""}>↓</button><button data-remove="${index}" aria-label="移除 ${escapeHtml(item.character)}">移除</button></span></li>`).join("") : `<li class="empty-state">确认区为空。点击候选图的“加入队列”。</li>`;
  document.querySelectorAll("[data-move]").forEach(button => button.addEventListener("click", () => { const i=Number(button.dataset.index), j=button.dataset.move==="up"?i-1:i+1; [state.queue[i],state.queue[j]]=[state.queue[j],state.queue[i]]; render(); }));
  document.querySelectorAll("[data-remove]").forEach(button => button.addEventListener("click", () => { state.queue.splice(Number(button.dataset.remove),1); render(); }));
}
function renderCandidates() {
  const query=$("#artwork-search").value.trim().toLowerCase();
  const queued=new Set(state.queue.map(keyOf));
  const visible=state.items.filter(item => !query || [item.character,item.franchise,item.title,item.author].join(" ").toLowerCase().includes(query));
  $("#candidate-list").innerHTML=visible.map(item => { const queuedNow=queued.has(keyOf(item)); return `<article class="art-card ${queuedNow?"queued":""}">${imageMarkup(item)}<div class="art-card-body"><div class="art-title"><strong>${escapeHtml(item.character || "未标注角色")}</strong><span>${escapeHtml(gameName(item.franchise))}</span></div><p>${escapeHtml(item.title || "无标题")}</p><p class="muted">作者：${escapeHtml(item.author || "未知")}</p><div class="art-actions"><a href="${escapeHtml(item.sourceUrl)}" target="_blank" rel="noreferrer">打开 Pixiv</a><button data-add="${escapeHtml(keyOf(item))}" ${queuedNow?"disabled":""}>${queuedNow?"已在队列":"加入队列"}</button></div></div></article>`; }).join("") || `<p class="empty-state">没有匹配候选。</p>`;
  document.querySelectorAll("[data-add]").forEach(button => button.addEventListener("click", () => { const item=state.items.find(x=>keyOf(x)===button.dataset.add); if(item){state.queue.push(item);render();} }));
}
function render(){ renderQueue(); renderCandidates(); }
function download() { const blob=new Blob([JSON.stringify(queuePayload(),null,2)+"\n"],{type:"application/json"}); const a=document.createElement("a"); a.href=URL.createObjectURL(blob);a.download="artwork-queue.json";a.click();URL.revokeObjectURL(a.href); }
async function save() {
  const api=(state.config.apiBase || "").replace(/\/$/,"");
  if(!api){ download(); showNotice("审核结果已下载。要从网页直接保存到仓库，请按仓库 docs/v4/WEB_EDITOR_SETUP.md 部署 GitHub OAuth Worker。", "warning"); return; }
  showNotice("正在保存到 GitHub…");
  const response=await fetch(`${api}/api/artwork-queue`,{method:"PUT",credentials:"include",headers:{"content-type":"application/json"},body:JSON.stringify({queue:queuePayload()})});
  if(response.status===401){ window.location.href=`${api}/api/login?returnTo=${encodeURIComponent(location.href)}`; return; }
  const result=await response.json().catch(()=>({}));
  if(!response.ok) throw new Error(result.error || "保存失败");
  showNotice(`已提交到 GitHub：${result.commitUrl || "等待 Actions 校验"}`, "success");
}
async function start(){
  const [items, queue, config]=await Promise.all([fetch("data/artwork-review.json",{cache:"no-store"}).then(r=>r.json()),fetch("data/artwork-queue.json",{cache:"no-store"}).then(r=>r.json()),fetch("data/editor-config.json",{cache:"no-store"}).then(r=>r.json())]);
  state.items=items;state.config=config;const lookup=new Map(items.map(x=>[keyOf(x),x]));state.queue=queue.sort((a,b)=>a.queueOrder-b.queueOrder).map(entry=>lookup.get(keyOf(entry))).filter(Boolean);
  $("#github-edit").href=`https://github.com/${config.repository}/edit/${config.branch}/data/artwork-queue.json`;
  render();showNotice("可在此审核、排序并暂存队列。保存方式取决于是否已配置安全编辑服务。", "success");
}
$("#artwork-search").addEventListener("input",renderCandidates);$("#download-queue").addEventListener("click",download);$("#save-queue").addEventListener("click",()=>save().catch(error=>showNotice(error.message,"error")));
start().catch(error=>showNotice(`审核数据读取失败：${error.message}`,"error"));
