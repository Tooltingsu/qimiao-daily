import { copyFile, mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { extname, resolve } from "node:path";
import { tmpdir } from "node:os";
import { sha256 } from "./chunking.mjs";
import { validateArtworkDownload } from "./artwork-media.mjs";

const root = resolve(process.env.GITHUB_WORKSPACE || process.cwd());
const date = process.env.INPUT_REPORT_DATE || "";
const revisionInput = process.env.INPUT_REVISION || "";
const pagesBaseUrl = (process.env.ARTWORK_RELAY_PAGES_BASE_URL || "https://tooltingsu.github.io/qimiao-daily").replace(/\/$/, "");

function extensionFor(contentType) {
  const type = String(contentType).toLowerCase().split(";", 1)[0];
  return ({ "image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp", "image/gif": ".gif" })[type] || ".img";
}

export function relayFolder(reportDate, revision) {
  return `assets/artwork-relay/${reportDate}/r${String(revision).padStart(3, "0")}`;
}

async function main() {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error("reportDate 必须为 yyyy-MM-dd。");
  const manifest = JSON.parse(await readFile(resolve(root, "reports", date, "manifest.json"), "utf8"));
  const revision = Number(revisionInput || manifest.lockedRevision);
  if (!Number.isInteger(revision) || revision !== manifest.lockedRevision) throw new Error("只允许为该日期的 locked revision 准备美图中转。");
  const report = JSON.parse(await readFile(resolve(root, "reports", date, "revisions", `${String(revision).padStart(3, "0")}.json`), "utf8"));
  if (sha256(report.content) !== manifest.reportHash || report.reportHash !== manifest.reportHash) throw new Error("LOCKED_REVISION_HASH_MISMATCH：禁止准备中转图片。");
  if (!Array.isArray(report.selectedArtwork) || report.selectedArtwork.length === 0) throw new Error("当前 locked revision 没有已选美图。");

  const folder = relayFolder(date, revision);
  const destination = resolve(root, "web", folder);
  const temporary = resolve(tmpdir(), `qimiao-v4-relay-${process.pid}-${Date.now()}`);
  await rm(destination, { recursive: true, force: true });
  await mkdir(destination, { recursive: true });
  try {
    const items = [];
    for (const artwork of report.selectedArtwork) {
      const downloaded = await validateArtworkDownload(artwork, temporary);
      const file = `${artwork.artworkId}${extensionFor(downloaded.contentType)}`;
      await copyFile(downloaded.filePath, resolve(destination, file));
      items.push({
        platform: artwork.platform,
        artworkId: artwork.artworkId,
        character: artwork.character,
        franchise: artwork.franchise,
        file,
        contentType: downloaded.contentType,
        bytes: downloaded.bytes,
        sha256: sha256(await readFile(resolve(destination, file))),
        relayUrl: `${pagesBaseUrl}/${folder}/${file}`
      });
    }
    await writeFile(resolve(destination, "manifest.json"), JSON.stringify({
      reportDate: date,
      revision,
      reportHash: report.reportHash,
      generatedAt: new Date().toISOString(),
      retention: "TEMPORARY_PAGES_RELAY; remove manually only after QQ visibility is confirmed",
      items
    }, null, 2) + "\n", "utf8");
    console.log(JSON.stringify({ status: "RELAY_PREPARED", date, revision, folder, images: items.length }));
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
}

if (import.meta.url === new URL(process.argv[1], "file:").href) await main();
