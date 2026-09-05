# V4 Architecture

## Runtime shape

V4 is repository-native:

```text
manual JSON ─┐
collected ───┼─> validate -> calculate -> generate immutable revision
calculated ──┘                                  |
                                                  v
                                      manual lock or deadline lock
                                                  |
                                                  v
                                  publish watchdog -> QQ publisher
                                                  |
                                                  v
                                        publish-log + Pages
```

GitHub Pages is a static projection of repository state. It does not mutate data and does not hold credentials. Edit and run buttons link to GitHub data files or Actions.

## Repository boundaries

- `data/`: manually maintained source of truth.
- `collected/`: automatic source snapshots and provider health.
- `generated/`: deterministic calendar/endgame output.
- `reports/`: report revisions, preview, and manifest.
- `publish-log/`: append-only publication attempts.
- `src/QimiaoDaily.V4/`: cross-platform CLI POC; no WPF reference.
- `web/`: static control center.
- `schemas/`: executable JSON contracts.
- `legacy-desktop/`: reserved for V3 after migration acceptance; V3 remains in place during V4-A.

## Workflow responsibilities

| Workflow | Trigger | Writes | Secrets |
| --- | --- | --- | --- |
| `validate.yml` | PR/push | Nothing | None |
| `collect.yml` | 17:17 Shanghai equivalent/manual | `collected/` | None in BGI POC |
| `calculate.yml` | data change/schedule/manual | `generated/` | None |
| `generate.yml` | source change/schedule/manual | `reports/`, `web/data/` | None |
| `publish.yml` | watchdog/manual | lock + `publish-log/` | None in DRY_RUN POC |
| `republish.yml` | explicit manual force | revision + log | None in DRY_RUN POC |
| `deploy-pages.yml` | `web/` change/manual | GitHub Pages deployment | OIDC Pages token only |

All repository-writing workflows, including publishing and republishing, share one non-cancelling concurrency group to prevent stale-checkout push races. The persisted date/hash publication guard remains the authoritative duplicate-send defense.

## Scheduler design

GitHub documents that scheduled workflows can be delayed under load and queued jobs can be dropped. V4 therefore does not treat cron as the publishing clock. `publish.yml` runs repeatedly around the configured deadline. Each run asks the CLI whether Shanghai time is inside the allowed window and whether a successful publication already exists.

The POC uses explicit UTC cron values for transparency. GitHub now also documents timezone-aware schedules, but the application-level Shanghai guard remains authoritative.

References:

- [GitHub Actions scheduled-event behavior](https://docs.github.com/en/actions/how-tos/troubleshoot-workflows)
- [Events that trigger workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)
- [Creating a GitHub Pages site](https://docs.github.com/en/pages/getting-started-with-github-pages/creating-a-github-pages-site)

## Degraded source behavior

Collectors write one of `HEALTHY`, `LOGIN_REQUIRED`, `RATE_LIMITED`, `BLOCKED`, or `FAILED`. If an earlier valid snapshot exists, a failing collector retains it and records `usedCachedData=true`. Generator health becomes `DEGRADED`, but valid manual data can still produce and publish a complete report.

Schema/semantic failures, report generation failures, lock failures, and QQ authentication failures are blocking.
