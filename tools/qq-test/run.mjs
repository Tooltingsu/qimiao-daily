import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { QQBot } from "@tencent-connect/qqbot-nodejs";
import { ApiError } from "@tencent-connect/qqbot-nodejs/protocol";
import { chunkReport, sha256 } from "./chunking.mjs";
import { forumImagePayload, forumRichTextPayload, forumThreadPayload, forumTitle } from "./forum.mjs";

const root = resolve(process.env.GITHUB_WORKSPACE || process.cwd());
const mode = process.env.INPUT_MODE || "auth";
const date = process.env.INPUT_REPORT_DATE || new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Shanghai", year: "numeric", month: "2-digit", day: "2-digit"
}).format(new Date());
const revisionInput = process.env.INPUT_REVISION || "";
const targetType = (process.env.QQ_TEST_TARGET_TYPE || "").toUpperCase();
const channelId = process.env.QQ_TEST_CHANNEL_ID || "";
const guildId = process.env.QQ_TEST_GUILD_ID || "";
const appId = process.env.QQ_BOT_APP_ID || "";
const appSecret = process.env.QQ_BOT_APP_SECRET || "";
const workflowRun = process.env.GITHUB_SERVER_URL && process.env.GITHUB_REPOSITORY && process.env.GITHUB_RUN_ID
  ? `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`
  : "LOCAL_QQ_TEST";
const testLogPath = process.env.QQ_TEST_LOG_PATH || resolve(root, "test-publish-log", `${date}.json`);

const result = {
  date,
  environment: "qq-test",
  mode,
  targetType: targetType || null,
  status: "RUNNING",
  workflowRun,
  reportRevision: null,
  reportHash: null,
  textChunks: [],
  messages: [],
  mediaCount: 0,
  attemptedAt: new Date().toISOString(),
  completedAt: null,
  error: null
};

function mask(value) {
  return String(value)
    .replaceAll(appSecret, "***")
    .replace(/QQBot\s+[A-Za-z0-9._~+/=-]+/gi, "QQBot ***")
    .replace(/access_token[=:\s]+[A-Za-z0-9._~+/=-]+/gi, "access_token=***")
    .slice(0, 400);
}

function failure(error) {
  if (error instanceof ApiError) {
    return `QQ API 调用失败：HTTP ${error.httpStatus}${error.bizCode ? `，错误码 ${error.bizCode}` : ""}${error.bizMessage ? `，${mask(error.bizMessage)}` : ""}`;
  }
  return mask(error instanceof Error ? error.message : error);
}

function configured(needsTarget) {
  if (!appId || !appSecret) return "缺少 qq-test Environment Secret：QQ_BOT_APP_ID 或 QQ_BOT_APP_SECRET。";
  if (!needsTarget) return null;
  if (!["CHANNEL", "FORUM"].includes(targetType)) return "qq-test 仅允许 QQ_TEST_TARGET_TYPE=CHANNEL 或 FORUM。";
  if (!channelId) return "缺少 qq-test Environment Variable：QQ_TEST_CHANNEL_ID。";
  if (targetType === "FORUM" && !guildId) return "论坛测试还需要 qq-test Environment Variable：QQ_TEST_GUILD_ID。";
  return null;
}

function logger() {
  return {
    info(message) { console.log(mask(message)); },
    warn(message) { console.warn(mask(message)); },
    error(message) { console.error(mask(message)); }
  };
}

async function loadLockedRevision() {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error("reportDate 必须为 yyyy-MM-dd。");
  const manifest = JSON.parse(await readFile(resolve(root, "reports", date, "manifest.json"), "utf8"));
  const revision = revisionInput ? Number(revisionInput) : manifest.lockedRevision;
  if (!Number.isInteger(revision) || revision < 1 || manifest.lockedRevision !== revision) {
    throw new Error("测试完整日报必须指定并使用该日期已锁定的 Revision。");
  }
  const record = JSON.parse(await readFile(resolve(root, "reports", date, "revisions", `${String(revision).padStart(3, "0")}.json`), "utf8"));
  const actualHash = sha256(record.content);
  if (actualHash !== manifest.reportHash || actualHash !== record.reportHash) {
    throw new Error("LOCKED_REVISION_HASH_MISMATCH：禁止发送。");
  }
  result.reportRevision = revision;
  result.reportHash = actualHash;
  return record;
}

function recordTargetResponse(response, chunk, kind) {
  if (targetType === "FORUM") {
    const postTaskId = response?.task_id;
    if (!postTaskId) throw new Error("QQ 论坛发帖接口未返回 task_id。帖子可能仍在审核，不能伪造已发布状态。");
    result.messages.push({ sequence: chunk.sequence, kind, postTaskId, createTime: response?.create_time ?? null, hash: chunk.hash });
    return;
  }
  const messageId = response?.id;
  if (!messageId) throw new Error("QQ 频道消息接口未返回 message ID。");
  result.messages.push({ sequence: chunk.sequence, kind, messageId, hash: chunk.hash });
}

async function sendWithRetry(send, chunk, kind = "text") {
  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      const response = await send();
      recordTargetResponse(response, chunk, kind);
      return;
    } catch (error) {
      const retryable = error instanceof ApiError && (error.httpStatus === 429 || error.httpStatus >= 500);
      if (!retryable || attempt === 3) throw error;
      await new Promise(resolve => setTimeout(resolve, 1000 * attempt));
    }
  }
}

function sendText(bot, modeName, chunk, total) {
  if (targetType === "FORUM") {
    return bot.api.put(
      `/channels/${encodeURIComponent(channelId)}/threads`,
      forumThreadPayload(forumTitle(modeName, date, chunk.sequence, total), chunk.text, 3));
  }
  return bot.sendChannelMessage(channelId, chunk.text);
}

function successfulSendStatus() {
  // The forum API returns a task_id. It acknowledges queueing, not a visible
  // thread ID; only the audit event or read-back can prove final visibility.
  return targetType === "FORUM" ? "TEST_SUBMITTED" : "TEST_PUBLISHED";
}

async function persist() {
  result.completedAt = new Date().toISOString();
  await mkdir(dirname(testLogPath), { recursive: true });
  let existing = { date, environment: "qq-test", attempts: [] };
  try {
    const parsed = JSON.parse(await readFile(testLogPath, "utf8"));
    if (Array.isArray(parsed.attempts)) existing = parsed;
  } catch { /* first test for this date */ }
  existing.date = date;
  existing.environment = "qq-test";
  existing.attempts.push(result);
  await writeFile(testLogPath, JSON.stringify(existing, null, 2) + "\n", "utf8");
  console.log(JSON.stringify({ status: result.status, mode: result.mode, date: result.date, messages: result.messages.length, error: result.error }));
}

try {
  const configError = configured(mode !== "auth");
  if (configError) {
    result.status = "BLOCKED_BY_USER";
    result.error = configError;
  } else {
    const bot = new QQBot({ appId, appSecret, logger: logger() });
    // Direct REST token/API calls only: no gateway, webhook, listener or long-lived process.
    await bot.api.getToken();
    if (mode === "auth") {
      result.status = "AUTHENTICATED";
    } else if (mode === "text") {
      const text = "【测试】绮喵日报 V4-C QQ 官方机器人连接测试";
      const chunk = { sequence: 1, text, hash: sha256(text) };
      result.textChunks = [{ sequence: chunk.sequence, hash: chunk.hash, characters: chunk.text.length }];
      await sendWithRetry(() => sendText(bot, "text", chunk, 1), chunk);
      result.status = successfulSendStatus();
    } else if (mode === "medium") {
      const text = "【测试】绮喵日报 V4-C 中等长度文本测试\n" + "本消息用于验证 QQ 官方机器人在测试目标的文本承载与稳定分段能力。\n".repeat(25);
      const chunks = chunkReport(text, Number(process.env.QQ_TEST_MAX_TEXT_CHARS || "1800"));
      result.textChunks = chunks.map(({ sequence, hash, text: chunkText }) => ({ sequence, hash, characters: chunkText.length }));
      for (const chunk of chunks) {
        await sendWithRetry(() => sendText(bot, "medium", chunk, chunks.length), chunk);
        await new Promise(resolve => setTimeout(resolve, 250));
      }
      result.status = successfulSendStatus();
    } else if (mode === "long") {
      const text = "【测试】绮喵日报 V4-C 长文本测试\n" + "本消息用于验证 QQ 官方机器人在测试目标的文本承载与稳定分段能力。\n".repeat(55);
      const chunks = chunkReport(text, Number(process.env.QQ_TEST_MAX_TEXT_CHARS || "1800"));
      result.textChunks = chunks.map(({ sequence, hash, text: chunkText }) => ({ sequence, hash, characters: chunkText.length }));
      for (const chunk of chunks) {
        await sendWithRetry(() => sendText(bot, "long", chunk, chunks.length), chunk);
        await new Promise(resolve => setTimeout(resolve, 250));
      }
      result.status = successfulSendStatus();
    } else if (mode === "richtext") {
      const text = "【测试】绮喵日报 V4-C 富文本结构测试：仅包含官方 RichText 的文本元素，用于定位图片测试失败原因。";
      const chunk = { sequence: 1, text, hash: sha256(text) };
      result.textChunks = [{ sequence: chunk.sequence, hash: chunk.hash, characters: chunk.text.length }];
      if (targetType === "FORUM") {
        await sendWithRetry(() => bot.api.put(
          `/channels/${encodeURIComponent(channelId)}/threads`,
          forumRichTextPayload(forumTitle("richtext", date), text)), chunk, "richtext");
      } else {
        await sendWithRetry(() => bot.sendChannelMessage(channelId, text), chunk, "richtext");
      }
      result.status = successfulSendStatus();
    } else if (mode === "report") {
      const revision = await loadLockedRevision();
      const chunks = chunkReport(revision.content, Number(process.env.QQ_TEST_MAX_TEXT_CHARS || "1800"));
      result.textChunks = chunks.map(({ sequence, hash, text: chunkText }) => ({ sequence, hash, characters: chunkText.length }));
      for (const chunk of chunks) {
        await sendWithRetry(() => sendText(bot, "report", chunk, chunks.length), chunk);
        await new Promise(resolve => setTimeout(resolve, 250));
      }
      result.status = successfulSendStatus();
    } else if (mode === "image") {
      const image = process.env.QQ_TEST_IMAGE_URL || "";
      if (!/^https:\/\//.test(image)) throw new Error("图片测试需要 HTTPS 的 QQ_TEST_IMAGE_URL。");
      const chunk = { sequence: 1, hash: sha256(image) };
      if (targetType === "FORUM") {
        await sendWithRetry(() => bot.api.put(
          `/channels/${encodeURIComponent(channelId)}/threads`,
          forumImagePayload(forumTitle("image", date), "【测试】绮喵日报 V4-C 图片连接测试", image)), chunk, "image");
      } else {
        await sendWithRetry(() => bot.api.post(
          `/channels/${encodeURIComponent(channelId)}/messages`,
          { content: "【测试】绮喵日报 V4-C 图片连接测试", image }), chunk, "image");
      }
      result.mediaCount = 1;
      result.status = successfulSendStatus();
    } else {
      throw new Error(`不支持的 qq-test mode：${mode}`);
    }
  }
} catch (error) {
  result.status = "TEST_FAILED";
  result.error = failure(error);
}

await persist();
if (!["AUTHENTICATED", "TEST_PUBLISHED", "TEST_SUBMITTED"].includes(result.status)) process.exitCode = 2;
