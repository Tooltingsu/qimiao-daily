import assert from "node:assert/strict";
import test from "node:test";
import { matchingForumThreads, renderForumVerificationMarkdown } from "../forum-verify.mjs";

test("forum verification only exposes matching titles and thread metadata", () => {
  const response = { threads: [
    { thread_info: { title: "【测试】绮喵日报 V4-C 连接测试", thread_id: "thread-1", date_time: "2026-09-06T10:00:00+08:00", content: "不应显示" } },
    { thread_info: { title: "普通帖子", thread_id: "thread-2" } }
  ] };
  const matches = matchingForumThreads(response, "【测试】绮喵日报 V4-C");
  assert.deepEqual(matches, [{ title: "【测试】绮喵日报 V4-C 连接测试", threadId: "thread-1", dateTime: "2026-09-06T10:00:00+08:00" }]);
  const markdown = renderForumVerificationMarkdown(matches, 2);
  assert.match(markdown, /匹配测试帖：1/);
  assert.doesNotMatch(markdown, /不应显示/);
});
