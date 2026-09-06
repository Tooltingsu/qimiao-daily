import test from "node:test";
import assert from "node:assert/strict";
import { chunkReport, sha256 } from "../chunking.mjs";

test("chunks deterministically at section boundaries", () => {
  const content = "标题\n日期\n\n活动 A\n活动 B\n\nBGI 更新";
  const first = chunkReport(content, 12);
  const second = chunkReport(content, 12);
  assert.deepEqual(first, second);
  assert.equal(first.map(x => x.text).join("\n\n"), content);
  assert.deepEqual(first.map(x => x.sequence), [1, 2, 3]);
  assert.ok(first.every(x => x.hash === sha256(x.text)));
});

test("does not cut an individual report item", () => {
  assert.throws(() => chunkReport("这是一个不能被从中间截断的活动条目", 8), /QQ_SECTION_ITEM_TOO_LONG/);
});
