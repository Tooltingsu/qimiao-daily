const stateLabels = {
  NOT_GENERATED: "尚未生成", READY: "已生成 · 待自动发布", LOCKED_MANUAL: "已人工锁定",
  LOCKED_AUTO: "已自动锁定", PUBLISHED: "已真实发布", DRY_RUN_SUCCEEDED: "演练完成",
  REPUBLICATION_READY: "准备重新发布", SUPERSEDED: "已被后续版本替代", FAILED: "失败"
};

const providerStatusLabels = {
  HEALTHY: "正常", DEGRADED: "部分来源异常", LOGIN_REQUIRED: "需要登录凭据",
  RATE_LIMITED: "请求受限", BLOCKED: "来源访问受阻", FAILED: "失败", UNKNOWN: "未知"
};

const providerLabels = {
  "Video:GENSHIN": "原神官方视频",
  "Video:STARRAIL": "星铁官方视频",
  "Video:NTE": "异环官方视频",
  "Pixiv": "Pixiv 美图"
};

const qqTestStatusLabels = {
  NOT_TESTED: "尚未测试", BLOCKED_BY_USER: "等待测试环境配置", AUTHENTICATED: "鉴权成功",
  TEST_PUBLISHED: "测试发布成功", TEST_FAILED: "测试发布失败", PARTIAL_FAILURE: "部分发送失败"
};

async function loadDashboard() {
  const [dashboardResponse, reportResponse] = await Promise.all([
    fetch("data/dashboard.json", { cache: "no-store" }),
    fetch("data/report.txt", { cache: "no-store" })
  ]);
  if (!dashboardResponse.ok) throw new Error("控制中心数据尚未生成");
  const data = await dashboardResponse.json();
  const report = reportResponse.ok ? await reportResponse.text() : "今日日报尚未生成。";
  document.querySelector("#report-date").textContent = `${data.date} · Asia/Shanghai`;
  document.querySelector("#state").textContent = stateLabels[data.state] ?? data.state;
  document.querySelector("#publish-time").textContent = data.publishTime;
  document.querySelector("#revision").textContent = data.revision;
  document.querySelector("#generated-at").textContent = formatTime(data.generatedAt);
  document.querySelector("#health").textContent = providerStatusLabels[data.health] ?? data.health;
  document.querySelector("#artwork-pending").textContent = data.artworkPending;
  document.querySelector("#conflicts").textContent = data.conflictCount;
  renderQqTest(data.qqTest);
  document.querySelector("#report-preview").textContent = report;
  document.querySelector("#state-dot").className = `status-dot ${data.state === "FAILED" ? "failed" : data.health === "HEALTHY" ? "healthy" : ""}`;
  renderMetrics("#manual-counts", data.manualCounts);
  renderMetrics("#automatic-counts", data.automaticCounts);
  renderProviders(data.providers);
  const repo = data.repositoryUrl.replace(/\/$/, "");
  document.querySelector("#edit-data").href = `${repo}/tree/main/data`;
  document.querySelector("#run-generate").href = `${repo}/actions/workflows/generate.yml`;
  document.querySelector("#lock-report").href = `${repo}/actions/workflows/lock.yml`;
  document.querySelector("#republish-report").href = `${repo}/actions/workflows/republish.yml`;
  document.querySelector("#view-actions").href = `${repo}/actions`;
}

function renderQqTest(test) {
  const value = test ?? { environment: "qq-test", status: "NOT_TESTED", messageCount: 0 };
  document.querySelector("#qq-test-environment").textContent = value.environment === "qq-test" ? "qq-test（测试环境）" : "未知环境";
  document.querySelector("#qq-test-status").textContent = qqTestStatusLabels[value.status] ?? value.status;
  document.querySelector("#qq-test-messages").textContent = `${value.messageCount ?? 0}${value.mediaCount ? `（含 ${value.mediaCount} 张图片）` : ""}`;
  document.querySelector("#qq-test-detail").textContent = value.error
    ? value.error
    : value.completedAt ? `最近测试：${formatTime(value.completedAt)}${value.mode ? ` · ${value.mode}` : ""}` : "测试发布不会影响正式发布记录。";
}

function renderMetrics(selector, metrics) {
  document.querySelector(selector).innerHTML = Object.entries(metrics)
    .map(([name, count]) => `<div><span>${escapeHtml(name)}</span><strong>${count}</strong></div>`).join("");
}

function renderProviders(providers) {
  if (!providers?.length) return;
  document.querySelector("#providers").innerHTML = providers.map(item => `
    <div class="provider-row"><div><strong>${escapeHtml(providerLabels[item.provider] ?? item.provider)}</strong><p>${escapeHtml(item.message)}</p></div>
    <span class="provider-status ${item.status === "HEALTHY" ? "" : "bad"}">${escapeHtml(providerStatusLabels[item.status] ?? item.status)}</span></div>`).join("");
}

function formatTime(value) {
  if (!value) return "尚未生成";
  return new Intl.DateTimeFormat("zh-CN", { timeZone: "Asia/Shanghai", hour12: false, month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value).replace(/[&<>'"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character]);
}

document.querySelector("#copy-report").addEventListener("click", async event => {
  await navigator.clipboard.writeText(document.querySelector("#report-preview").textContent);
  event.currentTarget.textContent = "已复制";
  setTimeout(() => event.currentTarget.textContent = "复制日报", 1500);
});

loadDashboard().catch(error => {
  document.querySelector("#state").textContent = "数据不可用";
  document.querySelector("#report-preview").textContent = error.message;
  document.querySelector("#state-dot").className = "status-dot failed";
});
