# V4 Security Model

## Trust zones

1. Pull requests and Issue content are untrusted input. Validation runs read-only and receives no secrets.
2. Default-branch calculation/generation is trusted but still receives no QQ or Pixiv secret unless its exact collector requires one.
3. Future production publishing runs only default-branch code in a protected GitHub Environment.
4. GitHub Pages is public/static output and receives no secret-bearing files.

## Secret rules

- QQ AppID/AppSecret and Pixiv Session belong in GitHub Environment or Actions Secrets.
- Secrets must never be written to JSON, report revisions, artifacts, logs, command-line echo, or Pages.
- Production jobs must use least-privilege `permissions` and protected environments.
- The POC publisher refuses `--dry-run false` and reports `BLOCKED_BY_USER`.

## Pull request isolation

V4 does not use `pull_request_target`. GitHub warns that privileged workflows which check out and execute untrusted PR code can expose repository write tokens and secrets. The validation workflow uses `pull_request`, read-only contents permission, and no secrets.

Reference: [Securely using pull_request_target](https://docs.github.com/en/actions/reference/security/securely-using-pull_request_target)

## Pages boundary

GitHub Pages publishes static files and does not support server-side PHP, Ruby, or Python. The V4 page only fetches generated JSON/text and links to GitHub-native edit/workflow pages. It contains no PAT and does not claim to be an authenticated write backend.

Reference: [Creating a GitHub Pages site](https://docs.github.com/en/pages/getting-started-with-github-pages/creating-a-github-pages-site)

## QQ blocker

Tencent's current official Python SDK documents AppID + AppSecret authentication. Real forum/message capability, payload limits, image upload, and returned post/message identifiers must be verified with the user's approved bot and test channel. Required values remain `BLOCKED_BY_USER` during V4-A.

References:

- [Tencent QQ bot SDK](https://github.com/tencent-connect/botpy)
- [Tencent QQ bot documentation repository](https://github.com/tencent-connect/bot-docs)
