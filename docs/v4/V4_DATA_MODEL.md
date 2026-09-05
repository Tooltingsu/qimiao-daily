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

Git stores artwork ID, character, franchise, author, Pixiv URL, thumbnail URL, review status, and selection state. It does not store bulk original images. A production publish job will download only `selectedForReport=true` images to runner temporary storage, upload them, and clean the workspace.
