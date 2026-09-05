# Actions security audit

## Permissions and trust

| Workflow | Trigger trust | Permissions | Secrets | Rationale |
| --- | --- | --- | --- | --- |
| validate | PR or main push | contents: read | none | Untrusted PR code receives no write token or secret. |
| collect | main schedule/dispatch | contents: write | optional PIXIV_SESSION only | Writes only collected/. GitHub API uses ephemeral GITHUB_TOKEN. |
| calculate | main data push/schedule/dispatch | contents: write | none | Writes only generated/. |
| generate | main data/collected/generated push/schedule/dispatch | contents: write | none | Writes reports/ and generated/ only. |
| lock | trusted main dispatch | contents: write | none | Writes report manifest/revision only. |
| publish / republish | trusted main dispatch/schedule | contents: write | no QQ secrets | V4-B dry run only, writes reports/publish-log. |
| deploy-pages | main push or trusted workflow_run | contents: read, pages: write, id-token: write | none | Only uploads the scanned web/ tree. |

No workflow uses `write-all`, `pull_request_target`, an external third-party write action, a PAT, or a QQ credential. Future real QQ publishing belongs to a protected `production` Environment, trusted default-branch code only, with `QQ_BOT_APP_ID` and `QQ_BOT_SECRET` scoped solely to that job.

## Action supply chain

| Action | Pinned major | Source | Purpose |
| --- | --- | --- | --- |
| actions/checkout | v4 | GitHub | Checkout trusted main or PR source. |
| actions/setup-dotnet | v4 | GitHub | Install .NET 8. |
| actions/configure-pages | v5 | GitHub | Configure Pages metadata. |
| actions/upload-pages-artifact | v3 | GitHub | Upload scanned static tree. |
| actions/deploy-pages | v4 | GitHub | Deploy Pages artifact. |

Each is an official GitHub action and necessary. No additional marketplace action was introduced.

## Write and secret safeguards

`tools/v4/runtime.py` fingerprints `data/*.json` before/after automation. `tools/v4/writeback.py` accepts only a declared output path set, fetches/rebases before up to three normal pushes, and aborts conflicts. It never force-pushes or resolves a conflict by overwriting remote files. The scanner redacts matches and fails candidate content or Pages artifacts containing credential-like data.
