# V4 Publish State Machine

## Normal path

```text
DRAFT -> VALIDATED -> READY -> LOCKED_MANUAL -> PUBLISHING -> PUBLISHED
                          \-> LOCKED_AUTO   -> PUBLISHING -> PUBLISHED
```

Confirmation means `READY -> LOCKED_MANUAL`. It is not permission to publish. If no manual lock exists at the configured deadline, the watchdog locks the latest valid revision as `LOCKED_AUTO` and continues.

## Immutability

Lock writes `lockedRevision`, `lockedAt`, and `lockReason` to `manifest.json`. Later collection or generation may create a newer READY revision, but publication continues to read the locked revision number and hash. The POC test creates revision 2 after locking revision 1 and proves that the publication attempt still uses revision 1.

## Idempotency

Normal publication refuses to proceed if either guard is true:

1. `publish-log/<date>.json` already contains a successful publication for the date.
2. The same `date + reportHash` already has a successful attempt.

Workflow concurrency reduces races; the persisted guards are still authoritative. Repeated watchdog executions exit without sending again.

## Correction path

```text
PUBLISHED -> SUPERSEDED -> REPUBLICATION_READY -> READY revision N+1
          -> LOCKED_MANUAL -> PUBLISHING -> PUBLISHED
```

The user manually deletes the incorrect QQ post, fixes repository data, checks the new Pages preview, and runs `republish.yml` with `force=true` and a reason. V4-A does not make automatic deletion a requirement.

## Failure classes

Blocking failures stop before a send: invalid JSON/semantics, generation failure, lock failure, missing locked revision, and QQ authentication failure.

Degraded collector failures do not block when a valid cached snapshot exists. Their status is embedded in the revision and visible on Pages.
