import test from "node:test";
import assert from "node:assert/strict";
import { deliverPendingChunks, pendingChunks } from "../send-plan.mjs";

const chunks = [
  { sequence: 1, hash: "sha256:one", text: "第一段" },
  { sequence: 2, hash: "sha256:two", text: "第二段" },
  { sequence: 3, hash: "sha256:three", text: "第三段" }
];

test("mid-send failure records partial state and resume does not repeat delivered chunks", async () => {
  const firstPass = [];
  const failed = await deliverPendingChunks({
    chunks,
    send: async chunk => {
      firstPass.push(chunk.sequence);
      if (chunk.sequence === 3) throw new Error("simulated QQ timeout");
      return { kind: "text", postTaskId: `task-${chunk.sequence}` };
    }
  });

  assert.deepEqual(firstPass, [1, 2, 3]);
  assert.equal(failed.status, "PARTIAL_FAILURE");
  assert.equal(failed.failureSequence, 3);
  assert.deepEqual(failed.messages.map(x => x.sequence), [1, 2]);

  const resumed = [];
  const completed = await deliverPendingChunks({
    chunks,
    delivered: failed.messages,
    send: async chunk => {
      resumed.push(chunk.sequence);
      return { kind: "text", postTaskId: `task-${chunk.sequence}` };
    }
  });
  assert.deepEqual(resumed, [3]);
  assert.equal(completed.status, "COMPLETE");
  assert.deepEqual(completed.messages.map(x => x.sequence), [1, 2, 3]);
});

test("resume rejects a chunk whose immutable hash changed", () => {
  assert.throws(() => pendingChunks(chunks, [{ sequence: 1, hash: "sha256:other" }]), /QQ_RESUME_HASH_MISMATCH/);
});
