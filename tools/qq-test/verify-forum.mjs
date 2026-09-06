import { writeFile } from "node:fs/promises";
import { QQBot } from "@tencent-connect/qqbot-nodejs";
import { ApiError } from "@tencent-connect/qqbot-nodejs/protocol";
import { matchingForumThreads, renderForumVerificationMarkdown } from "./forum-verify.mjs";

const appId = process.env.QQ_BOT_APP_ID || "";
const appSecret = process.env.QQ_BOT_APP_SECRET || "";
const targetType = (process.env.QQ_TARGET_TYPE || process.env.QQ_TEST_TARGET_TYPE || "").toUpperCase();
const channelId = process.env.QQ_TARGET_CHANNEL_ID || process.env.QQ_TEST_CHANNEL_ID || "";
const titlePrefix = process.env.QQ_TEST_TITLE_PREFIX || "【测试】绮喵日报 V4-C";
const output = process.env.FORUM_VERIFY_OUTPUT || process.env.GITHUB_STEP_SUMMARY;
const jsonOutput = process.env.FORUM_VERIFY_JSON_OUTPUT || "";

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

let markdown;
try {
  if (!appId || !appSecret) throw new Error("缺少 qq-test Environment Secret：QQ_BOT_APP_ID 或 QQ_BOT_APP_SECRET。");
  if (targetType !== "FORUM") throw new Error("论坛核验仅允许 QQ_TEST_TARGET_TYPE=FORUM。");
  if (!channelId) throw new Error("缺少 qq-test Environment Variable：QQ_TEST_CHANNEL_ID。");
  const bot = new QQBot({ appId, appSecret, logger: { info() {}, error() {} } });
  await bot.api.getToken();
  // Official read-only forum list endpoint. No gateway and no message/thread creation.
  const response = await bot.api.get(`/channels/${encodeURIComponent(channelId)}/threads`);
  const all = Array.isArray(response?.threads) ? response.threads : [];
  const matches = matchingForumThreads(response, titlePrefix);
  markdown = renderForumVerificationMarkdown(matches, all.length);
  if (jsonOutput) await writeFile(jsonOutput, JSON.stringify({ titlePrefix, matchCount: matches.length, matches }, null, 2) + "\n", "utf8");
  if (!matches.length) process.exitCode = 3;
} catch (error) {
  markdown = `# QQ 论坛测试帖核验失败\n\n${safeError(error)}\n\n未发送 QQ 消息；未输出 AppSecret 或 access token。`;
  process.exitCode = 2;
}

if (output) await writeFile(output, markdown + "\n", "utf8");
console.log(markdown);
