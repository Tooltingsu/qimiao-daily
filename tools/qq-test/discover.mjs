import { writeFile } from "node:fs/promises";
import { QQBot } from "@tencent-connect/qqbot-nodejs";
import { ApiError } from "@tencent-connect/qqbot-nodejs/protocol";
import { renderDiscoveryMarkdown, rowsForGuilds } from "./discovery-format.mjs";

const appId = process.env.QQ_BOT_APP_ID || "";
const appSecret = process.env.QQ_BOT_APP_SECRET || "";
const output = process.env.DISCOVERY_OUTPUT || process.env.GITHUB_STEP_SUMMARY;

function safeError(error) {
  if (error instanceof ApiError) {
    return `QQ API 调用失败：HTTP ${error.httpStatus}${error.bizCode ? `，错误码 ${error.bizCode}` : ""}${error.bizMessage ? `，${String(error.bizMessage).slice(0, 160)}` : ""}`;
  }
  return String(error instanceof Error ? error.message : error)
    .replaceAll(appSecret, "***")
    .replace(/QQBot\s+[A-Za-z0-9._~+/=-]+/gi, "QQBot ***")
    .replace(/access_token[=:\s]+[A-Za-z0-9._~+/=-]+/gi, "access_token=***")
    .slice(0, 400);
}

async function listAll(bot, path) {
  const results = [];
  let after = "";
  for (let page = 0; page < 20; page++) {
    const query = after ? { limit: 100, after } : { limit: 100 };
    const response = await bot.api.get(path, query);
    const batch = Array.isArray(response) ? response : (Array.isArray(response?.data) ? response.data : []);
    results.push(...batch);
    if (batch.length < 100 || !batch.at(-1)?.id) break;
    after = String(batch.at(-1).id);
  }
  return results;
}

let markdown;
try {
  if (!appId || !appSecret) throw new Error("缺少 qq-test Environment Secret：QQ_BOT_APP_ID 或 QQ_BOT_APP_SECRET。");
  const bot = new QQBot({ appId, appSecret, logger: { info() {}, error() {} } });
  // Direct official REST only. No gateway, webhook or message send is started.
  await bot.api.getToken();
  const guilds = await listAll(bot, "/users/@me/guilds");
  const channelsByGuild = new Map();
  for (const guild of guilds) {
    channelsByGuild.set(String(guild.id), await listAll(bot, `/guilds/${encodeURIComponent(guild.id)}/channels`));
  }
  markdown = renderDiscoveryMarkdown(rowsForGuilds(guilds, channelsByGuild));
} catch (error) {
  markdown = `# QQ 测试目标发现失败\n\n${safeError(error)}\n\n未发送 QQ 消息；未输出 AppSecret 或 access token。`;
  process.exitCode = 2;
}

if (output) await writeFile(output, markdown + "\n", "utf8");
console.log(markdown);
