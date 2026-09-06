import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, access } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { resolveArtworkImageUrl, withValidatedArtwork } from "../artwork-media.mjs";

test("selected artwork is validated temporarily and then removed", async () => {
  const folder = await mkdtemp(join(tmpdir(), "qimiao-qq-image-"));
  let tempPath;
  const result = await withValidatedArtwork(
    [{ artworkId: "sample", thumbnailUrl: "https://example.test/a.png" }], folder,
    async ([image]) => { tempPath = image.filePath; await access(tempPath); return image; },
    async () => new Response(new Uint8Array([137, 80, 78, 71]), { status: 200, headers: { "content-type": "image/png" } })
  );
  assert.equal(result.bytes, 4);
  await assert.rejects(access(tempPath));
});

test("uses a verified-at-runtime Pixiv master-preview candidate for legacy metadata", () => {
  assert.equal(
    resolveArtworkImageUrl({ platform: "PIXIV", artworkId: "149119754", thumbnailUrl: "", publishedAt: "2026-08-31T21:16:39+09:00" }),
    "https://i.pximg.net/img-master/img/2026/08/31/21/16/39/149119754_p0_master1200.jpg"
  );
});

test("missing direct image URL blocks before text could be sent", async () => {
  await assert.rejects(
    withValidatedArtwork([{ artworkId: "149119754", thumbnailUrl: "", sourceUrl: "https://www.pixiv.net/artworks/149119754" }], join(tmpdir(), "qimiao-not-used"), async () => {}),
    /PUBLISH_MEDIA_FAILED/
  );
});
