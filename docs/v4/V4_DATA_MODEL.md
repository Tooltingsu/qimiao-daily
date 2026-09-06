# V4 Data Model

## Ownership classes

| Class | Directory | Mutability |
| --- | --- | --- |
| MANUAL | `data/` | Edited through GitHub and reviewed like code |
| AUTO | `collected/` | Replaced by collectors; metadata only |
| CALCULATED | `generated/` | Rebuilt deterministically from MANUAL inputs |
| REPORT | `reports/` | New revisions may be added; existing revision files are immutable |
| PUBLICATION | `publish-log/` | Append-only attempts |

## Manual files

- `activities.json`: id, game, name, start/end, notes, enabled.
- `banners.json`: id, game, name, type, start/end, ordered characters, notes, enabled.
- `versions.json`: id, game, version number/name, start/end, notes, enabled.
- `endgame-rules.json`: stable rule id, independent anchor, interval/kind, precision, optional start time.
- `endgame-overrides.json`: per-rule correction keyed by scheduled start.
- `birthdays.json`: character, franchise, month/day, enabled state, provenance.
- `anniversaries.json`: title, original start date, enabled state, notes.
- `calendar-events.json`: one-off manual memorial/custom date entries retained separately from recurring anniversaries.
- `artwork-queue.json`: reviewed artwork IDs in their user-selected FIFO order.
- `settings.json`: timezone, publish time, repository navigation, non-secret target identifiers.

Every file above is checked by a JSON Schema in `schemas/` and by semantic validation. Schema-valid but logically invalid intervals are rejected.

## DATE_ONLY invariant

`timePrecision=DATE_ONLY` is authoritative. Such records must keep `startTime=null`; no synthetic `00:00` or `04:00` may be introduced. The V4 calculator calls the existing V3 recurrence engine, which already tests this behavior.

## Report revision

A revision contains:

```json
{
  "date": "2026-09-05",
  "revision": 3,
  "state": "READY",
  "sourceCommit": "abc123",
  "reportHash": "sha256:...",
  "generatedAt": "2026-09-05T09:42:00Z",
  "lockedAt": null,
  "lockReason": null,
  "publishedAt": null,
  "content": "...",
  "health": "HEALTHY",
  "providerStatuses": []
}
```

`sourceCommit` identifies the input snapshot. `reportHash` identifies the exact formatted content. Publishing reads the locked revision file only; it never regenerates from live data.

## Artwork policy

`collected/artwork.json` stores artwork metadata only. `data/artwork-queue.json` is the manual confirmed area: it stores an artwork ID/platform and its `queueOrder`. This separation means an automatic collection refresh cannot change the user's confirmation or daily sequence. Git does not store bulk original images.

The reviewed **confirmed area is a FIFO queue**. A generated/locked report snapshots **only the first image**. It does not consume it. A real production publisher downloads that image only to runner temporary storage, uploads it to QQ, and cleans the temporary file. Only after every required report part and image has a confirmed `PUBLISHED` result may it delete that exact entry from `data/artwork-queue.json`; dry runs and `qq-test` never advance or delete the queue.
