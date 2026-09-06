export function cleanupPlan(response, titlePrefix) {
  const threads = Array.isArray(response?.threads) ? response.threads : [];
  return threads
    .map(item => item?.thread_info ?? item)
    .filter(item => String(item?.title ?? "").startsWith(titlePrefix) && String(item?.thread_id ?? ""))
    .map(item => ({ title: String(item.title), threadId: String(item.thread_id) }));
}

export function renderCleanupMarkdown(plan, isFinish) {
  return [
    "# QQ 论坛测试帖清理",
    "",
    `可删除的 V4-C 测试帖：${plan.length}；列表完整：${Number(isFinish) === 1 ? "是" : "否/未知"}。`,
    "",
    "清理只匹配标题前缀 `【测试】绮喵日报 V4-C`，不会匹配或删除正式日报。"
  ].join("\n");
}
