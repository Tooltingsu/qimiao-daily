import assert from "node:assert/strict";
import test from "node:test";
import { cleanupPlan, renderCleanupMarkdown } from "../cleanup-forum.mjs";

test("cleanup plan only selects explicitly marked V4-C forum posts", () => {
  const response = { is_finish: 1, threads: [
    { thread_info: { title: "【测试】绮喵日报 V4-C 图片测试", thread_id: "test-1" } },
    { thread_info: { title: "绮喵日报 260906", thread_id: "production-1" } },
    { thread_info: { title: "【测试】其他项目", thread_id: "other-1" } }
  ] };
  const plan = cleanupPlan(response, "【测试】绮喵日报 V4-C");
  assert.deepEqual(plan, [{ title: "【测试】绮喵日报 V4-C 图片测试", threadId: "test-1" }]);
  assert.match(renderCleanupMarkdown(plan, response.is_finish), /可删除的 V4-C 测试帖：1/);
});
