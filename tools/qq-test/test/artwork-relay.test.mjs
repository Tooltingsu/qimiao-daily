import assert from "node:assert/strict";
import test from "node:test";
import { relayFolder } from "../artwork-relay.mjs";

test("Pages artwork relay has a stable revision-specific public folder", () => {
  assert.equal(relayFolder("2026-09-06", 4), "assets/artwork-relay/2026-09-06/r004");
});
