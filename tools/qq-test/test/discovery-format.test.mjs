import test from "node:test";
import assert from "node:assert/strict";
import { renderDiscoveryMarkdown, rowsForGuilds } from "../discovery-format.mjs";

test("discovery highlights type zero channels and keeps target IDs", () => {
  const rows = rowsForGuilds([{ id: "g1", name: "测试|频道" }], new Map([["g1", [
    { id: "c1", name: "文字", type: 0 }, { id: "c2", name: "论坛", type: 10007 }, { id: "c3", name: "语音", type: 2 }
  ]]]));
  const markdown = renderDiscoveryMarkdown(rows);
  assert.match(markdown, /文字子频道（type=0）：1/);
  assert.match(markdown, /测试\\\|频道/);
  assert.match(markdown, /\| c1 \| 0 \|/);
  assert.match(markdown, /论坛\/帖子子频道（type=10007）/);
  assert.match(markdown, /\| c2 \| 10007 \|/);
  assert.match(markdown, /QQ_TEST_GUILD_ID/);
});
