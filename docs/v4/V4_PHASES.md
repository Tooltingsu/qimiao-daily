# V4 Delivery Phases

## V4-A — Audit and proof of concept

Deliverables in this phase:

- Cross-platform CLI separated from WPF.
- V3 read-only export to repository JSON.
- JSON Schema and semantic validation.
- Existing recurrence engine executed from JSON.
- Real BGI commit collection.
- Immutable revision, manual/automatic lock, idempotent DRY_RUN, and forced revision 2.
- Static Pages control center.
- GitHub Actions definitions.
- Security and blocker documentation.

Exit condition: local tests/build and POC evidence pass. No real QQ post.

## V4-B — Repository commissioning

1. Create/select the GitHub repository and set `repositoryUrl`.
2. Push V4-A to the protected default branch.
3. Enable Pages through Actions.
4. Reconcile the exported disabled legacy `GAME` calendar rows and add an optional historical audit archive.
5. Add Pixiv and official video collectors to Actions with provider health retention.
6. Observe at least seven days of collection stability.

## V4-C — QQ test-channel integration

1. User supplies approved bot AppID/AppSecret through a protected Environment.
2. User supplies channel/forum identifiers.
3. Implement actual QQ API adapter behind the existing publish service.
4. Verify text length, image upload, forum-post behavior, API return IDs, retry safety, and rate limits.
5. Execute only in a non-production test channel.

## V4-D — Production shadowing

GitHub generates and dry-runs daily while V3 remains available for comparison. Any mismatch blocks Desktop retirement.

## V4-E — Production cutover

Enable real scheduled QQ publishing, retain manual republish, monitor for seven successful days, then request explicit user approval to archive V3 under `legacy-desktop/`.

## Current blockers

- GitHub repository owner/name and default-branch protection are not configured.
- Pages URL is unknown; `data/settings.json` retains an OWNER placeholder.
- QQ bot AppID/AppSecret, target channel, target forum, and API capability test are absent.
- Pixiv Session is not configured in GitHub Secrets, and runner stability has not been measured.
- Production QQ publishing remains intentionally disabled.
