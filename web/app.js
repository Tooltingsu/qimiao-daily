const stateLabels = {
  NOT_GENERATED: "尚未生成", READY: "已生成 · 待自动发布", LOCKED_MANUAL: "已人工锁定",
  LOCKED_AUTO: "已自动锁定", PUBLISHED: "已发布", FAILED: "失败"
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
  document.querySelector("#health").textContent = data.health;
  document.querySelector("#artwork-pending").textContent = data.artworkPending;
  document.querySelector("#conflicts").textContent = data.conflictCount;
  document.querySelector("#report-preview").textContent = report;
  document.querySelector("#state-dot").className = `status-dot ${data.state === "FAILED" ? "failed" : data.health === "HEALTHY" ? "healthy" : ""}`;
  renderMetrics("#manual-counts", data.manualCounts);
  renderMetrics("#automatic-counts", data.automaticCounts);
  renderProviders(data.providers);
  const repo = data.repositoryUrl.replace(/\/$/, "");
  document.querySelector("#edit-data").href = `${repo}/tree/main/data`;
  document.querySelector("#run-generate").href = `${repo}/actions/workflows/generate.yml`;
  document.querySelector("#view-actions").href = `${repo}/actions`;
}

function renderMetrics(selector, metrics) {
  document.querySelector(selector).innerHTML = Object.entries(metrics)
    .map(([name, count]) => `<div><span>${escapeHtml(name)}</span><strong>${count}</strong></div>`).join("");
}

function renderProviders(providers) {
  if (!providers?.length) return;
  document.querySelector("#providers").innerHTML = providers.map(item => `
    <div class="provider-row"><div><strong>${escapeHtml(item.provider)}</strong><p>${escapeHtml(item.message)}</p></div>
    <span class="provider-status ${item.status === "HEALTHY" ? "" : "bad"}">${escapeHtml(item.status)}</span></div>`).join("");
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
