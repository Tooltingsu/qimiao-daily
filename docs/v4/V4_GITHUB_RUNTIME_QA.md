# GitHub runtime QA

Status: **PASS — V4-B hosted-runtime validation.** Every entry below is a real GitHub-hosted Linux result. `DRY_RUN_SUCCEEDED` means no QQ API call was made and `publishedAt`, QQ post ID and QQ message ID remain `null`.

| Workflow | Run ID | Commit / date | Expected | Actual | Status |
| --- | ---: | --- | --- | --- | --- |
| Validate | [33974422701](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33974422701) | `730ea6f` | Portable V4 build, validation and tests on Ubuntu | Final V4-B commit: .NET 8 build, Core/Collectors/V4 tests and semantic validation passed. | PASS |
| Calculate | [33956484700](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33956484700) | 2026-09-05 | Compute endgame/calendar and write generated data | Real runner write-back succeeded. | PASS |
| Collect | [33956486504](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33956486504) | 2026-09-05 | BGI window and media provider states | BGI main=3, scripts=1 in `[D-1 18:00,D 18:00)` Shanghai window; Genshin/Star Rail HEALTHY; NTE 412 recorded; Pixiv LOGIN_REQUIRED. | PASS / DEGRADED recorded |
| Generate | [33972790575](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33972790575) | 2026-09-05 | Immutable revision and Pages projection | Revision 005 generated, committed and projected; confirmed artwork snapshot preserved. | PASS |
| Deploy Pages | [33973416872](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973416872) | `e65cedb` | Scan and deploy static tree | Production deploy succeeded after `secret_scan.py --tree web`. | PASS |
| Manual lock | [33972899180](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33972899180) | 2026-09-06 | `READY → LOCKED_MANUAL` | Revision 001 locked with source commit, hash and lock time. | PASS |
| Post-lock generate | [33972973203](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33972973203) | 2026-09-06 | Preserve locked revision and add draft | Revision 002 added; revision 001 SHA-256 remained `2640E4D8AE494A54087901BBBF7938827E49A90C435A3C8E82EFEE4F88AEFA89`. | PASS |
| Auto lock + publish dry run | [33973093254](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973093254) | 2026-09-07 simulation | Deadline lock and record dry run only | `lockReason=AUTO_DEADLINE`, one dry-run attempt, no QQ IDs. | PASS |
| Duplicate publish | [33973243339](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973243339) | 2026-09-07 | Same date/hash is a successful no-op | `SKIPPED_ALREADY_PUBLISHED`; no second ordinary attempt. | PASS |
| Republish dry run | [33973293292](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973293292) | 2026-09-07 | Corrected revision + append-only history | Revision 002 and second dry-run attempt created; revision 001 retained. | PASS |
| Concurrent generate ×2 | [33973363060](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973363060), [33973364895](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973364895) | 2026-09-08 | Serialize same-date generation | Both succeeded sequentially as revisions 001 then 002, without race or overwrite. | PASS |
| Write-back conflict QA | [33973508360](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33973508360) | disposable remote branch | Safe rebase and conflict abort/no force | Different paths rebased/pushed normally; same path conflicted, rebase aborted and remote was retained. Test branch cleaned up. | PASS |

## Runtime fixes made from actual evidence

1. Initial Ubuntu Validate (`33945320863`) tried to restore Windows Desktop and hit `NETSDK1100`; portable validation now only builds/tests V4/Core/Collectors.
2. Initial Calculate (`33945320855`) exposed stale plain-push rejection. `writeback.py` now path-allow-lists outputs, fetches first, skips needless rebases, rebases a truly advanced remote, retries normal pushes and never force-pushes.
3. Linux generation exposed an omitted `web/data/` root and legacy desktop cache paths in public metadata. The root is allow-listed, reports/exporter accept HTTPS thumbnails only, and local absolute paths now fail scanning.
4. A duplicate watchdog dry-run could race the precheck. Its final idempotency guard now returns `SKIPPED_ALREADY_PUBLISHED` rather than failing the workflow.

## Provider evidence

| Provider | Actual hosted state | Notes |
| --- | --- | --- |
| better-genshin-impact | HEALTHY, 3 | Provisional Shanghai window stored in `collected/bgi-window.json`. |
| bettergi-scripts-list | HEALTHY, 1 | Same date-specific window. |
| Official Genshin video | HEALTHY, 15 candidates | Official YouTube RSS. |
| Official Star Rail video | HEALTHY, 15 candidates | Official YouTube RSS. |
| Official NTE video | FAILED (HTTP 412) | Real Bilibili response, no fixture substitution. |
| Pixiv | LOGIN_REQUIRED | `PIXIV_SESSION` deliberately unset; cache remains usable, so report is DEGRADED rather than blocked. |

## Production Pages and scans

- Repository: <https://github.com/Tooltingsu/qimiao-daily> (public, default `main`).
- Pages: <https://tooltingsu.github.io/qimiao-daily/>.
- Repository candidate scan: `python tools/v4/secret_scan.py` = PASS.
- Pages deployment ran `python3 tools/v4/secret_scan.py --tree web` before upload = PASS.
- No QQ secret, Pixiv session, PAT, `.env`, database, browser profile, image cache or runner temporary artifact is committed/deployed.

## Real screenshots

All were captured from public GitHub/GitHub Pages URLs, not localhost:

1. `docs/v4/evidence/github-runtime/01-repository-root.png`
2. `docs/v4/evidence/github-runtime/02-actions-overview.png`
3. `docs/v4/evidence/github-runtime/03-validate-success.png`
4. `docs/v4/evidence/github-runtime/04-calculate-success.png`
5. `docs/v4/evidence/github-runtime/05-collect-success.png`
6. `docs/v4/evidence/github-runtime/06-generate-success.png`
7. `docs/v4/evidence/github-runtime/07-pages-deploy-success.png`
8. `docs/v4/evidence/github-runtime/08-pages-desktop.png`
9. `docs/v4/evidence/github-runtime/09-pages-mobile.png`
10. `docs/v4/evidence/github-runtime/10-manual-lock-success.png`
11. `docs/v4/evidence/github-runtime/11-publish-dry-run-success.png`
12. `docs/v4/evidence/github-runtime/12-republish-dry-run-success.png`
13. `docs/v4/evidence/github-runtime/13-publish-history.png`
14. `docs/v4/evidence/github-runtime/14-pages-secret-scan.png`

## Scope boundary

V4-B sent no QQ post/message. QQ production is `BLOCKED_BY_USER` pending V4-C credentials and explicit user approval. Pixiv may remain `LOGIN_REQUIRED` until the user elects to configure its GitHub Actions Secret.
