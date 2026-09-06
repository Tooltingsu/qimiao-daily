import assert from "node:assert/strict";
import test from "node:test";
import { forumImagePayload, forumThreadPayload, forumTitle } from "../forum.mjs";

test("forum text payload uses documented Markdown format", () => {
  assert.deepEqual(forumThreadPayload("【测试】标题", "正文"), {
    title: "【测试】标题", content: "正文", format: 3
  });
  assert.equal(forumTitle("report", "2026-09-06", 2, 3), "【测试】绮喵日报 V4-C 完整日报测试 2026-09-06（2/3）");
});

test("forum image payload uses documented RichText JSON image element", () => {
  const payload = forumImagePayload("【测试】图片", "图片说明", "https://example.test/image.png");
  assert.equal(payload.format, 4);
  const richText = JSON.parse(payload.content);
  assert.deepEqual(richText.paragraphs[0].elems[1], {
    type: 2, image: { third_url: "https://example.test/image.png", width_percent: 1 }
  });
});
