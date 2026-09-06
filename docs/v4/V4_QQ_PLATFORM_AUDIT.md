# V4-C QQ 官方机器人平台审计

审计日期：2026-09-06（Asia/Shanghai）  
结论：**已选定腾讯维护的官方 Node.js SDK；已确认测试目标为 type=10007 论坛子频道。真实网络、鉴权、论坛发帖和图片能力仍待 `qq-test` GitHub-hosted Runner 实测。**

## 采用的官方 SDK

| 项目 | 结论 |
| --- | --- |
| 包 | `@tencent-connect/qqbot-nodejs` |
| 固定版本 | `1.0.4` |
| 上游 | <https://github.com/tencent-connect/qqbot-nodejs> |
| 运行时 | Node.js 20+、纯 ESM；测试工作流使用 Node 24 |
| 认证 | 运行时用 AppID/AppSecret 向 `https://bots.qq.com/app/getAppAccessToken` 获取短期 access token；不会保存 access token |
| REST 基址 | `https://api.sgroup.qq.com` |
| 论坛目标 | `PUT /channels/{channel_id}/threads`；目标为论坛子频道 ID（type=10007），而不是将 ID 写入仓库 |
| 正常文字目标（兼容） | `POST /channels/{channel_id}/messages`；仅用于 type=0 文字子频道 |
| 真实返回 ID | 论坛创建任务的 `task_id`（以及 `create_time`）；正常文字频道才是 `MessageResponse.id` |

官方 SDK README 明确列出 HTTP REST、token 管理、消息与媒体能力，并要求 Node.js 20+；SDK 的 1.0.3 变更记录包含通用发送、媒体预检等近期能力。SDK 的原始 REST client 用于当前高层 SDK 未封装的官方论坛发帖 endpoint；token 在进程内缓存、由 AppID/AppSecret 换取。腾讯官方论坛文档规定创建帖子使用 `PUT /channels/{channel_id}/threads`，仅限私域机器人，内容 format 可为 Markdown（3）或 RichText JSON（4）。参考：[SDK README](https://github.com/tencent-connect/qqbot-nodejs)、[SDK CHANGELOG](https://github.com/tencent-connect/qqbot-nodejs/blob/main/CHANGELOG.md)、[腾讯官方论坛发帖接口](https://bot.q.qq.com/wiki/develop/api-v2/server-inter/channel/content/forum/put_thread.html)、[腾讯官方论坛 RichText 模型](https://bot.q.qq.com/wiki/develop/api-v2/server-inter/channel/content/forum/model.html)。

## 连接模型

日报是单次主动发布，不接收 QQ 消息。因此 V4-C 的测试工具直接调用 SDK 的 REST/token client：

`Auth → PUT 论坛帖子 → 进程退出`

不会启动常驻 Gateway、Webhook server 或消息监听器。SDK 本身提供 WebSocket/Webhook 是为入站事件服务，不是本用例的前置条件；是否存在平台侧“机器人必须在线”要求，必须由 GitHub-hosted Runner 的真实测试结果确认，不能以本地试验代替。

## 图片与长文本

- 当前 SDK 为正常文字子频道提供 `sendChannelMessage`，并提供受鉴权保护的原始 REST 出口。论坛子频道不能误调用文字消息 endpoint。
- 论坛文本用 Markdown `format=3` 创建主题；论坛图片测试用官方 `format=4` RichText JSON 的 `ImageElem.third_url`，使用 Pages 上的自制不透明 96×96 PNG。先前 1×1 透明 raw-GitHub 图片未能形成可见帖子，不能视为图片能力通过；成功或失败均以真实 API 返回为准。
- SDK/当前页面未给出可直接依赖的频道单条文本上限。本项目以可配置的保守值 `1800` 先做真实阶段测试，不宣称它是平台上限；完整日报会按段落/条目确定性分段，遇到单条目超限会阻止发送，绝不从活动名称中间截断。
- 每条分段保存 SHA-256；同一 locked revision 的分段结果确定不变。

## 限流、错误与网络

- SDK 定义结构化 `ApiError`，带 HTTP status 与 QQ 业务错误码；V4-C 只对网络错误、429 和 5xx 至多重试三次。400/401/403/参数错误不重试。
- 频道连续分段之间固定等待 250ms；真正的 `Retry-After` / QQ 返回限流语义由真实测试记录后再补充。
- 尚未找到可作为结论的当前官方“GitHub-hosted Runner 固定出口 IP / 白名单”依据。因此第一份真实 `qq-connectivity-test.yml` 输出是唯一有效 Gate：若 QQ 返回白名单/源 IP/ACL 拒绝，将记录 HTTP 状态、业务错误码和脱敏报错到 `V4_QQ_GITHUB_RUNNER_NETWORK_RESULT.md`，并停止生产 Publisher。

## 安全与环境分离

仅创建 `qq-test` GitHub Environment；未创建、未读取 `production` Environment。

用户需在 GitHub 仓库中配置，而不是在聊天或 Git 中提供：

`Repository → Settings → Environments → qq-test`

| 类型 | 名称 | 用途 |
| --- | --- | --- |
| Environment secret | `QQ_BOT_APP_ID` | 机器人 AppID |
| Environment secret | `QQ_BOT_APP_SECRET` | 机器人 AppSecret |
| Environment variable | `QQ_TEST_TARGET_TYPE` | 选中的 type=10007 论坛子频道填 `FORUM` |
| Environment variable | `QQ_TEST_GUILD_ID` | 已选测试 Guild ID（仅 runner 使用，不写入日志或 Pages） |
| Environment variable | `QQ_TEST_CHANNEL_ID` | 已选**测试**论坛子频道 ID（仅 runner 使用，不写入日志或 Pages） |

测试日志位于 `test-publish-log/`，而不是 `publish-log/`；因此任何 `qq-test` 发帖不会占用生产日期的幂等记录。日志和 Pages 只显示状态、分段 hash 与 API 返回的 `task_id` / `messageId`，绝不保存 AppSecret、access token 或完整测试目标 ID。

## 当前状态

| Gate | 状态 |
| --- | --- |
| 真实生产日期恢复 | PASS：Pages projection 已恢复为 2026-09-06，默认 workflow 空日期仍取 Shanghai 当日 |
| 官方 SDK 方案与最小 workflow | READY |
| qq-test 环境 | AppID/AppSecret 已配置；用户已选 type=10007 论坛目标，代码支持 `QQ_TEST_TARGET_TYPE=FORUM`，待写入目标变量 |
| GitHub-hosted Runner Auth / 只读 OpenAPI | VERIFIED：运行 [34006072572](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34006072572) 成功列出 1 个 Guild、33 个子频道和 2 个 type=0 文字子频道，未发送消息 |
| GitHub-hosted Runner 论坛发帖 | 仅得到创建 `task_id`；8 分钟后只读帖子列表仍无匹配测试帖，**不视为发布成功** |
| 论坛可见性核验 | 已实现只读 `GET /channels/{channel_id}/threads` 工作流；无匹配帖子会失败，`task_id` 仅代表创建任务被接收，不能单独等同于前台可见 |
| 当前阻塞 | 腾讯论坛发帖接口标注 `<PrivateDomain/>`，即仅私域机器人可用。选定的外部 Guild 论坛目标很可能因此在异步审核阶段未通过；在确认机器人为该 Guild 的私域机器人前，禁止继续发送长文本、图片或完整日报 |
| Production QQ 自动发布 | 仍关闭 |
