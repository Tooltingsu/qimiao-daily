# 网页美图审核器部署

`web/review.html` 已提供候选预览、确认队列、排序、移除与本地 JSON 下载；主页工作台也提供活动、卡池、版本、生日与纪念日的弹窗编辑。要让“保存审核结果”直接写入 GitHub，需要部署 `workers/github-editor`；GitHub Pages 本身不能安全持有写入凭据。

## 一次性配置

1. 在 GitHub **Settings → Developer settings → OAuth Apps → New OAuth App** 创建应用。
   - Homepage URL：`https://tooltingsu.github.io/qimiao-daily/`
   - Authorization callback URL：部署后的 Worker 地址 + `/api/callback`，例如 `https://qimiao-github-editor.<你的账户>.workers.dev/api/callback`。
2. 在 Cloudflare 创建 KV namespace；把其 ID 填入 `workers/github-editor/wrangler.toml`。
3. 将 `GITHUB_OAUTH_CLIENT_ID` 填入同一文件，并设置 Worker Secret（绝不写入仓库）：

```bash
cd workers/github-editor
npx wrangler secret put GITHUB_OAUTH_CLIENT_SECRET
npx wrangler deploy
```

4. Worker Variables 中确认：
   - `REPOSITORY=Tooltingsu/qimiao-daily`
   - `BRANCH=main`
   - `PAGES_ORIGIN=https://tooltingsu.github.io`
5. 编辑 `web/data/editor-config.json`，将 `apiBase` 写为 Worker 的 HTTPS 地址，提交后等待 Pages 部署。

OAuth Token 仅保留在 Worker KV 的一小时会话中，以 HttpOnly Cookie 标识；不会写入 Pages、仓库或 Actions 日志。网页只允许来自 `PAGES_ORIGIN` 的跨域请求。

## 审核操作

打开 Pages 的“美图审核”：选择候选图、加入确认区、用上下按钮排序，然后点击“保存审核结果”。保存会直接提交 `data/artwork-queue.json`；工作台保存会提交对应的人工 JSON。GitHub Actions 随后校验并生成新的日报 Revision。
