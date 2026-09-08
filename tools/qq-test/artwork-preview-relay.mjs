import { copyFile, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { tmpdir } from "node:os";
import { validateArtworkDownload } from "./artwork-media.mjs";

const root = resolve(process.env.GITHUB_WORKSPACE || process.cwd());
const limit = Number(process.env.INPUT_ARTWORK_PREVIEW_LIMIT || "30");
const keyOf = item => `${String(item.platform).trim()}\u001f${String(item.artworkId).trim()}`;
const extension = type => ({ "image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp", "image/gif": ".gif" })[String(type).toLowerCase().split(";", 1)[0]] || ".img";

async function main() {
  if (!Number.isInteger(limit) || limit < 1 || limit > 60) throw new Error("INPUT_ARTWORK_PREVIEW_LIMIT must be 1..60");
  const artworks = JSON.parse(await readFile(resolve(root, "collected/artwork.json"), "utf8"));
  const queue = JSON.parse(await readFile(resolve(root, "data/artwork-queue.json"), "utf8"));
  const byKey = new Map(artworks.map(item => [keyOf(item), item]));
  const queued = queue.sort((a, b) => a.queueOrder - b.queueOrder).map(item => byKey.get(keyOf(item))).filter(Boolean);
  const candidates = [...queued, ...artworks.filter(item => item.thumbnailUrl && !queued.some(queuedItem => keyOf(queuedItem) === keyOf(item)))];
  const destination = resolve(root, "web/assets/artwork-preview");
  const temporary = resolve(tmpdir(), `qimiao-v4-preview-${process.pid}-${Date.now()}`);
  await rm(destination, { recursive: true, force: true });
  await mkdir(destination, { recursive: true });
  const manifest = {};
  try {
    for (const item of candidates.slice(0, limit)) {
      try {
        // Fetch happens in Actions with Pixiv's required Referer. Browsers
        // cannot supply that header, which is why the static Pages UI uses this
        // short-lived same-origin thumbnail cache instead of hotlinking pximg.
        const downloaded = await validateArtworkDownload(item, temporary);
        if (downloaded.bytes > 2 * 1024 * 1024) continue;
        const file = `${item.platform.toLowerCase()}-${item.artworkId}${extension(downloaded.contentType)}`;
        await copyFile(downloaded.filePath, resolve(destination, file));
        manifest[keyOf(item)] = `assets/artwork-preview/${file}`;
      } catch (error) {
        console.warn(`Preview skipped ${item.artworkId}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    await writeFile(resolve(root, "web/data/artwork-preview.json"), JSON.stringify(manifest, null, 2) + "\n", "utf8");
    console.log(JSON.stringify({ status: "PREVIEW_RELAY_READY", count: Object.keys(manifest).length }));
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
}
await main();
