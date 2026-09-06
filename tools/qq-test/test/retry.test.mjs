import test from "node:test";
import assert from "node:assert/strict";
import { retryDelayMs } from "../retry.mjs";

test("uses Retry-After when a QQ transport exposes it", () => {
  assert.equal(retryDelayMs({ retryAfter: "2" }, 1), 2000);
  assert.equal(retryDelayMs({ retryAfterMs: 1500 }, 1), 1500);
  assert.equal(retryDelayMs({ headers: { get: key => key === "retry-after" ? "3" : null } }, 1), 3000);
});

test("uses bounded backoff when SDK does not expose a rate-limit header", () => {
  assert.equal(retryDelayMs({}, 1), 1000);
  assert.equal(retryDelayMs({}, 99), 10000);
});
