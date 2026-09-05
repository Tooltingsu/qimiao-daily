# V4 Migration Audit

Status: V4-A proof of concept. V3 Desktop is retained and must not be deleted.

## Executive finding

The V3 application already separates most business logic from WPF. The pivot is feasible without rewriting the proven recurrence engine, time normalization, report rules, or collectors. The main migration work is replacing EF Core/SQLite persistence and desktop commands with repository JSON, immutable report revisions, and GitHub Actions orchestration.

## Directly reusable components

| V3 component | V4 disposition | Reason |
| --- | --- | --- |
| `QimiaoDaily.Core` domain/time rules | Reuse | No WPF dependency |
| `EndgameScheduleEngine` and `EndgameScheduleRules` | Reuse now; move to Core later | Pure deterministic calculation; preserves DATE_ONLY semantics |
| `ChineseCalendarEngine` | Reuse/extend | Deterministic solar term and traditional calendar logic |
| `DailyReportFormatter` | Port formatting rules | Formatting is deterministic but currently accepts EF entities |
| `QimiaoImportService` validation concepts | Port to repository validator | Current persistence adapter is SQLite-specific |
| `GitHubCommitProvider` | Reuse | `HttpClient`-based and Action-compatible |
| `PixivArtworkProvider` | Reuse with runner health states | Requires session/rate-limit handling and must store metadata only |
| Official video providers | Reuse | Collectors are independent of WPF; persistence is not |
| WPF ViewModels and windows | Do not migrate | Replaced by Pages read-only control center and GitHub-native operations |

## Persistence migration

| SQLite source | V4 target | Required treatment |
| --- | --- | --- |
| `manual_events` | `data/activities.json` | Source of truth; Schema + semantic validation |
| `banners`, `banner_characters` | `data/banners.json` | Preserve character order |
| `game_versions` | `data/versions.json` | Preserve Shanghai offsets |
| `endgame_rules`, `endgame_anchors` | `data/endgame-rules.json` | Preserve independent anchors and DATE_ONLY |
| Rule configuration overrides | `data/endgame-overrides.json` | Key by `ruleId + scheduledStart` |
| `birthdays` | `data/birthdays.json` | Preserve enabled state and evidence |
| `anniversaries` | `data/anniversaries.json` | Preserve start year for anniversary count |
| `calendar_events` | `data/calendar-events.json` | Preserve manual calendar rows independently from recurring anniversaries |
| `artworks` | `collected/artwork.json` | Metadata only; discard local cache paths |
| Confirmed official video timeline rows | `collected/videos.json` | Preserve review status and evidence URL |
| `git_commit_records` | `collected/bgi-*.json` | Split main and scripts repositories |
| Review/revision/audit tables | Git history + optional migration archive | Git becomes the primary revision ledger |
| Report drafts | `reports/<date>/revisions/*.json` | Immutable after creation |
| Provider health and task runs | `collected/provider-status.json` and Actions run history | Separate source health from publication state |

## POC export result

The read-only exporter was executed against the current V3 database on 2026-09-05:

- 23 activities
- 6 banners
- 3 game versions
- 9 endgame rules
- 0 explicit endgame overrides
- 164 birthdays
- 0 anniversary table rows
- 33 calendar event rows (all are disabled legacy `GAME` rows; preserved for reconciliation but excluded from the report)
- 130 artwork metadata rows
- 34 official video/preview rows
- 23 BGI commit rows

The original database remains unchanged. The empty anniversary result is a factual property of the current `anniversaries` table, not a migration drop.

## Gaps discovered

1. V3 stores some audit history in SQLite. Git covers future changes, but a one-time archival export is still required if historical field-level actions must remain searchable.
2. Artwork metadata can migrate now, but GitHub-hosted runner access to Pixiv must be measured over time. A single successful local request is not sufficient evidence of production reliability.
3. QQ channel/forum posting payload limits and image upload behavior require a real approved bot and test channel. V4-A intentionally does not fabricate success.
4. The project directory is not currently a Git repository. Workflows and Pages are present but cannot run until the user creates/selects a GitHub repository and pushes the default branch.

## Retirement gate for V3

V3 Desktop can only move to `legacy-desktop/` after all of the following pass: complete data reconciliation, seven consecutive days of Action runs, real QQ test-channel publishing, corrected-report republishing, and user approval. Until then V3 remains the reference implementation.
