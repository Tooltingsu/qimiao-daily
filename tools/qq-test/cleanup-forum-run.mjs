import { writeFile } from "node:fs/promises";
import { QQBot } from "@tencent-connect/qqbot-nodejs";
import { ApiError } from "@tencent-connect/qqbot-nodejs/protocol";
import { cleanupPlan, renderCleanupMarkdown } from "./cleanup-forum.mjs";

const appId = process.env.QQ_BOT_APP_ID || "";
const appSecret = process.env.QQ_BOT_APP_SECRET || "";
const targetType = (process.env.QQ_TEST_TARGET_TYPE || "").toUpperCase();
const channelId = process.env.QQ_TEST_CHANNEL_ID || "";
const titlePrefix = process.env.QQ_TEST_TITLE_PREFIX || "【测试】绮喵日报 V4-C";
const confirmation = process.env.INPUT_CONFIRM_DELETE || "";
const output = process.env.FORUM_CLEANUP_OUTPUT || process.env.GITHUB_STEP_SUMMARY;

function safeError(error) {
  if (error instanceof ApiError) {
    return `QQ API 调用失败：HTTP ${error.httpStatus}${error.bizCode ? `，错误码 ${error.bizCode}` : ""}${error.bizMessage ? `，${String(error.bizMessage).replaceAll(appSecret, "***").slice(0, 160)}` : ""}`;
  }
  return String(error instanceof Error ? error.message : error)
    .replaceAll(appSecret, "***")
    .replace(/QQBot\s+[A-Za-z0-9._~+/=-]+/gi, "QQBot ***")
    .replace(/access_token[=:\s]+[A-Za-z0-9._~+/=-]+/gi, "access_token=***")
    .slice(0, 400);
}

async function deleteWithRetry(bot, threadId) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      await bot.api.delete(`/channels/${encodeURIComponent(channelId)}/threads/${encodeURIComponent(threadId)}`);
      return;
    } catch (error) {
      const retryable = error instanceof ApiError && (error.httpStatus === 429 || error.httpStatus >= 500);
      if (!retryable || attempt === 3) throw error;
      await new Promise(resolve => setTimeout(resolve, attempt * 1000));
    }
  }
}

let markdown;
try {
  if (confirmation !== "DELETE_TEST_POSTS") throw new Error("清理被阻止：INPUT_CONFIRM_DELETE 必须精确等于 DELETE_TEST_POSTS。");
  if (!appId || !appSecret) throw new Error("缺少 qq-test Environment Secret：QQ_BOT_APP_ID 或 QQ_BOT_APP_SECRET。");
  if (targetType !== "FORUM") throw new Error("论坛清理仅允许 QQ_TEST_TARGET_TYPE=FORUM。");
  if (!channelId) throw new Error("缺少 qq-test Environment Variable：QQ_TEST_CHANNEL_ID。");
  const bot = new QQBot({ appId, appSecret, logger: { info() {}, error() {} } });
  await bot.api.getToken();
  const response = await bot.api.get(`/channels/${encodeURIComponent(channelId)}/threads`);
  const plan = cleanupPlan(response, titlePrefix);
  markdown = renderCleanupMarkdown(plan, response?.is_finish);
  if (Number(response?.is_finish) !== 1) throw new Error("帖子列表未确认完整，拒绝执行清理以避免遗漏测试帖。");
  for (const item of plan) {
    await deleteWithRetry(bot, item.threadId);
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  markdown += `\n\n已删除：${plan.length} 条测试帖。`; 
} catch (error) {
  markdown = `# QQ 论坛测试帖清理失败\n\n${safeError(error)}\n\n未删除任何未确认的帖子；未输出 AppSecret 或 access token。`;
  process.exitCode = 2;
}

if (output) await writeFile(output, markdown + "\n", "utf8");
console.log(markdown);
