export function matchingForumThreads(response, titlePrefix) {
  const threads = Array.isArray(response?.threads) ? response.threads : [];
  return threads
    .map(item => item?.thread_info ?? item)
    .filter(item => String(item?.title ?? "").startsWith(titlePrefix))
    .map(item => ({
      title: String(item.title ?? ""),
      threadId: String(item.thread_id ?? ""),
      dateTime: String(item.date_time ?? "")
    }));
}

export function renderForumVerificationMarkdown(matches, total) {
  const rows = matches.length
    ? matches.map(item => `| ${escapeMarkdown(item.title)} | ${escapeMarkdown(item.threadId)} | ${escapeMarkdown(item.dateTime)} |`).join("\n")
    : "未在当前可读取的帖子列表中找到匹配测试帖。";
  return [
    "# QQ 论坛测试帖核验（只读）",
    "",
    `可读取帖子数：${total}；匹配测试帖：${matches.length}。`,
    "",
    "| 标题 | Thread ID | 发帖时间 |",
    "| --- | --- | --- |",
    rows,
    "",
    matches.length
      ? "结果：可通过官方读取接口看到测试帖子。"
      : "结果：创建接口返回 task_id 只代表已接受创建任务，不等同于帖子已可见；需结合论坛审核事件或频道内实际可见性继续诊断。",
    "",
    "此核验不发送 QQ 消息，也不输出 AppSecret 或 access token。"
  ].join("\n");
}

function escapeMarkdown(value) {
  return String(value).replace(/([\\`|])/g, "\\$1").replace(/[\r\n]+/g, " ");
}
