# V4-B execution plan

## Verified starting state (2026-09-05)

Local root: `E:/BaiduNetdiskDownload/qimiaobotv2`. No Git repository at takeover.
Remote verified with `gh repo view`: `Tooltingsu/qimiao-daily`, PRIVATE, empty, no default branch yet. First push will establish `main`.
README, AGENTS, V4 documents, all workflows, CLI, schemas and tests inspected. PROJECT_SPEC, REQUIREMENTS and PROGRESS files are absent; this plan and the attached V4-B objective define the phase.
V4-A has a .NET 8 CLI referencing Core/Data/Collectors/Services, nine manual schemas, read-only SQLite export, reused recurrence/calendar algorithms, BGI collector, report revisions and dry-run publication. V3 stays in place.

## Runtime validation sequence

1. Audit candidate Git content and credentials; initialize main, bind only the verified remote, commit a reproducible V4-A baseline.
2. Push baseline and record actual Linux workflow results. Repair workflow/platform defects using that evidence.
3. Add missing V4-B adapters and guards: explicit report-date BGI window; real official video calls; Pixiv LOGIN_REQUIRED; manual lock dispatch; honest DRY_RUN status; immutable text/image snapshot; complete semantic validation.
4. Centralize workflow execution, path-limited writes, fetch/rebase retry, summaries and shared writer concurrency. Use read-only PR jobs and trusted-main writer jobs.
5. Dispatch Validate, Calculate, Collect, Generate, Manual Lock, Publish and Republish; exercise auto-deadline simulation, immutable revisions, duplicate skip, two concurrent generators and a safe Git conflict test.
6. Deploy Pages through GitHub Actions; inspect actual uploaded artifact and live desktop/mobile rendering. Capture the required 14 evidence views.
7. Record run IDs, commit IDs, test counts, source statuses, growth estimates and every gate in V4_GITHUB_RUNTIME_QA.md. Stop at V4-B; no real QQ API requests.

## Risks and blockers

| Area | Observed risk | Mitigation / evidence required |
|---|---|---|
| Repository | No local Git, remote empty | Explicit remote verification, main initial branch, no other repository modifications |
| Pages | POST Pages returned HTTP 422: current plan does not support this private repository | User decision required: approve public visibility or upgrade private Pages plan; other work continues |
| Linux | validate restores WPF solution | Capture hosted failure, scope Linux build/tests to portable projects; retain desktop source/tests |
| Time | BGI uses latest completed window from runtime clock | Explicit report date and half-open Shanghai 18:00 bounds, boundary tests and live evidence |
| Concurrency | One pending slot can cancel excess pending runs; checkout SHA may be stale | Shared writer group, checkout latest default branch after waiting, test two simultaneous runs |
| Git write-back | Plain git push has no conflict retry | Path allowlist, no data/ writes, fetch/rebase safe retry, same-file conflicts fail loudly |
| Secrets | republish interpolates dispatch text into shell | Structured environment/argv input, scan candidates/artifact, no QQ secret references |
| Actions | bot pushes do not reliably chain workflows | Explicit workflow_run projection for Pages, bounded acyclic trigger graph |
| Revision | dry-run sets PUBLISHED, hash not verified, artwork not frozen | Honest simulation status, verify digest, capture selected artwork metadata at generation |
| Collectors | only BGI wired; no pagination/window upper bound verification | Reuse providers, add adapters, record real provider failures, preserve cache |
| Data | birthdays/day combinations and overlapping versions not checked | Negative runtime semantic tests; illegal data fail before generation |
| Storage | histories grow; original artwork must remain outside Git | Measure tracked sizes, estimate 1/3 years, preserve locked revisions and publication log |

## Acceptance

Every numbered requirement in the supplied V4-B objective remains in scope. Local green tests and YAML parsing are not runtime PASS. Required evidence is real hosted Ubuntu runs, actual production Pages, measured non-recursion/concurrency/conflict behavior, preserved manual data and V3, actual source responses, and zero QQ sends. Missing external entitlement keeps the phase incomplete.
