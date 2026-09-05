# GitHub Secrets and Environments

V4-B requires no secret to validate, calculate, generate, deploy Pages, lock, or dry-run publish.

| Name | Scope | Used by | Purpose |
| --- | --- | --- | --- |
| PIXIV_SESSION | repository secret | collect only | Optional Pixiv authenticated metadata search. Missing value produces LOGIN_REQUIRED and does not block the report. |
| QQ_BOT_APP_ID | future `production` Environment secret | future QQ publisher only | Official QQ bot application identifier. |
| QQ_BOT_SECRET | future `production` Environment secret | future QQ publisher only | Official QQ bot credential. |

Never put a value in JSON, Pages data, report revisions, issue forms, action input, command line, logs, screenshots, or `.env`. The repository does not need a PAT: BGI uses the runner `GITHUB_TOKEN` and write-back uses the job token.

Create the protected `production` Environment only in V4-C. Add reviewers there and keep all other workflows outside it. The current `qq-publish-dry-run` Environment contains no secret and intentionally performs no network request to QQ.
