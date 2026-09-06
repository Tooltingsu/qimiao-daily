import { mkdir, rm, writeFile } from "node:fs/promises";
import { basename, resolve } from "node:path";

const MAX_IMAGE_BYTES = 30 * 1024 * 1024;

function requireDirectImageUrl(artwork) {
  const url = String(artwork?.thumbnailUrl ?? "");
  if (!/^https:\/\//.test(url)) {
    // A Pixiv artwork page is intentionally not treated as an image URL. This
    // prevents an apparently successful text-only replacement for a selected
    // image when the collector did not retain a usable preview URL.
    throw new Error(`PUBLISH_MEDIA_FAILED：美图 ${artwork?.artworkId ?? "unknown"} 没有可用的 HTTPS 直接图片链接。`);
  }
  return url;
}

export async function validateArtworkDownload(artwork, directory, fetchImpl = fetch) {
  const url = requireDirectImageUrl(artwork);
  await mkdir(directory, { recursive: true });
  let response;
  try {
    response = await fetchImpl(url, {
      headers: { referer: "https://www.pixiv.net/", "user-agent": "QimiaoDaily-V4-C/1.0" },
      redirect: "follow"
    });
  } catch (error) {
    throw new Error(`PUBLISH_MEDIA_FAILED：美图 ${artwork.artworkId} 下载失败：${error instanceof Error ? error.message : String(error)}`);
  }
  const type = response.headers.get("content-type") ?? "";
  if (!response.ok || !type.toLowerCase().startsWith("image/")) {
    throw new Error(`PUBLISH_MEDIA_FAILED：美图 ${artwork.artworkId} 不是可下载图片（HTTP ${response.status}，${type || "未知内容类型"}）。`);
  }
  const bytes = Buffer.from(await response.arrayBuffer());
  if (!bytes.length || bytes.length > MAX_IMAGE_BYTES) {
    throw new Error(`PUBLISH_MEDIA_FAILED：美图 ${artwork.artworkId} 文件大小异常（${bytes.length} bytes）。`);
  }
  const filePath = resolve(directory, `${basename(String(artwork.artworkId || "artwork"))}.image`);
  await writeFile(filePath, bytes);
  return { artworkId: String(artwork.artworkId), sourceUrl: url, filePath, contentType: type, bytes: bytes.length };
}

// All validation artifacts are temporary runner files.  The `third_url`
// forum payload still uses the original HTTPS URL because the current forum
// RichText endpoint accepts a remote image URL, not a local file upload ID.
export async function withValidatedArtwork(artworks, directory, callback, fetchImpl = fetch) {
  try {
    const validated = [];
    for (const artwork of artworks) validated.push(await validateArtworkDownload(artwork, directory, fetchImpl));
    return await callback(validated);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}
