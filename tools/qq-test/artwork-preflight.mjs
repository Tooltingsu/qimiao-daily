import { appendFile, readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { tmpdir } from "node:os";
import { sha256 } from "./chunking.mjs";
import { withValidatedArtwork } from "./artwork-media.mjs";

const root = resolve(process.env.GITHUB_WORKSPACE || process.cwd());
const date = process.env.INPUT_REPORT_DATE || "";
const revisionInput = process.env.INPUT_REVISION || "";

async function summary(lines) {
  const text = ["# QimiaoDaily selected-artwork preflight", "", ...lines].join("\n") + "\n";
  if (process.env.GITHUB_STEP_SUMMARY) await appendFile(process.env.GITHUB_STEP_SUMMARY, text, "utf8");
  else console.log(text);
}

try {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error("reportDate 必须为 yyyy-MM-dd。");
  const manifest = JSON.parse(await readFile(resolve(root, "reports", date, "manifest.json"), "utf8"));
  const revision = revisionInput ? Number(revisionInput) : manifest.lockedRevision;
  if (!Number.isInteger(revision) || revision !== manifest.lockedRevision) throw new Error("只允许校验该日期的 locked revision。");
  const report = JSON.parse(await readFile(resolve(root, "reports", date, "revisions", `${String(revision).padStart(3, "0")}.json`), "utf8"));
  if (sha256(report.content) !== manifest.reportHash || report.reportHash !== manifest.reportHash) throw new Error("LOCKED_REVISION_HASH_MISMATCH：禁止校验发送素材。");
  if (!report.selectedArtwork?.length) throw new Error("当前 locked revision 没有已选美图。");
  const temp = resolve(tmpdir(), `qimiao-v4-artwork-${process.pid}-${Date.now()}`);
  const results = await withValidatedArtwork(report.selectedArtwork, temp, async items => items);
  await summary([
    `- 日期：${date}`,
    `- Revision：${revision}`,
    `- Hash：${manifest.reportHash}`,
    `- 已验证图片数：${results.length}`,
    ...results.map(item => `- 美图 ${item.artworkId}：${item.contentType}，${item.bytes} bytes（临时文件已清理）`),
    "- 结果：PREFLIGHT_PASSED（未读取 QQ 凭据、未调用 QQ API、未发送帖子）"
  ]);
} catch (error) {
  await summary([`- 结果：PUBLISH_MEDIA_FAILED`, `- 错误：${error instanceof Error ? error.message : String(error)}`, "- 未读取 QQ 凭据、未调用 QQ API、未发送帖子。"]);
  process.exitCode = 2;
}
