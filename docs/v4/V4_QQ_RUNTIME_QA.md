# V4-C QQ 官方机器人运行 QA

测试日期：2026-09-06（Asia/Shanghai）  
环境：`qq-test`（不读取 production Environment；不写入 `publish-log/`）

## 已验证的真实链路

| 项目 | 结果 | 运行证据 |
| --- | --- | --- |
| Auth only | PASS | [34007041491](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34007041491) |
| 最小论坛文本 | PASS，已由只读论坛 API 核验可见 | 发送 [34010432545](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010432545)，核验 [34010482215](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010482215) |
| 中等文本 | PASS | 发送 [34010804525](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010804525)，核验 [34010842731](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010842731) |
| 长文本 | PASS，确定性切成 1769 + 174 字两段 | 发送 [34010674210](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010674210)，核验 [34010713881](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34010713881) |
| RichText 文本 | PASS | 发送 [34011059500](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011059500)，核验 [34011084535](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011084535) |
| 自制测试图片 | PASS，使用 Pages 托管的自制不透明 PNG | 发送 [34011195182](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011195182)，核验 [34011249630](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011249630) |
| 锁定完整日报 | PASS，Revision 1 / `sha256:508aaadb46c5b413540e83f1b3dcba9b50ded1764782a23288d75af8c3a760e0` | 发送 [34011285052](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011285052)，核验 [34011322311](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34011322311) |

论坛创建返回的真实字段是 `task_id`，不是普通文字子频道 `messageId`。这些 `postTaskId` 已只写入 `test-publish-log/2026-09-06.json`；测试日志不会占用正式 `publish-log/` 的幂等记录。

## 安全与恢复验证

- `publish.yml` 与 `republish.yml` 仍是明确标识的 **DRY RUN**；V4-C 没有启用生产 QQ 发布或定时读取 `qq-test`。
- Node 单元测试以 fake transport 模拟第三段发送失败：状态为 `PARTIAL_FAILURE`，恢复只发送未完成的第三段，已成功的前两段不会重复。
- C# V4 测试覆盖普通发布幂等与 `republish` 生成 revision 2；qq-test Pages 投影与生产 `publish-log/` 隔离。
- `tools/v4/secret_scan.py` 覆盖 `QQ_BOT_APP_SECRET`、`Authorization`、`access_token`，运行结果必须为 PASS 才允许提交。
- [34018101053](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018101053) 已在 GitHub-hosted Runner 通过 Core、Collectors、V4、QQ Node harness（12 项）及 Secret Scan；后续每次 `main` 推送都会执行该 QQ harness。

## 美图的当前 Gate

测试图片已通过。当前 locked revision 的已选 Pixiv 美图没有收集到 `thumbnailUrl`；运行时会基于其 immutable work ID 与记录的 JST 发布时间生成一个**候选** master-preview URL，但绝不写回仓库，也绝不将它直接视为成功。只有临时下载、MIME/大小校验和 QQ 图片发送全部成功，才算图片成功；否则为 `PUBLISH_MEDIA_FAILED`，并且不会悄悄替换成普通文本或测试图。仓库不会保存 Pixiv 原图。

旧版直接 URL 策略已用真实 `qq-test` workflow 验证：[34017924578](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34017924578) 在准备发送之前返回 `PUBLISH_MEDIA_FAILED`；测试日志记录 `messages: []`、`mediaCount: 0`，证明失败时没有先发送文本日报，也没有新建测试帖子。现已改为候选 master-preview URL 的下载校验策略；它尚未运行真实发送测试，避免在机器人尚无删帖权限时重新留下测试帖。

`qq-artwork-preflight.yml` 是仅手动、无 QQ Secret、无 QQ API 调用的前置校验：它验证 locked revision hash 后才临时下载图片，并总在退出时清理临时文件。只有该 Gate 通过，才有资格请求一次需要人工删帖配合的实际图片发送测试。

实际发送已于 [34018791395](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018791395) 执行：locked Revision 1 的文本与一张已选 Pixiv 美图均收到 QQ 论坛 `task_id`。随后 [34018840573](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018840573) 和 [34018924256](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018924256) 的只读列表确认了文本帖可见，但当前读取窗口没有对应图片帖。之后又以图片帖的精确标题进行只读核验：[34039795981](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34039795981)，仍未找到该帖；因此测试日志被明确标记为 `TEST_PARTIAL_VISIBILITY`，**不把图片 task_id 误写成图片发布成功**。这说明当前可用能力已可可靠发布文本，但尚不能证明 Pixiv 直链可由 QQ 论坛服务端取图并展示。

## 独立 qq-test 目标复验（2026-09-07）

已确认 `qq-test` 与 `production` 配置为不同的论坛子频道；工作流仍只读取 `qq-test` Environment。2026-09-07 通过 GitHub Environments 变量的脱敏比较复核：两侧目标字段都已配置、目标类型和 Guild 相同、`sameChannel=false`；因此测试发帖不会落到正式日报子频道，且本文不记录任何目标 ID。

| 阶段 | 结果 | 运行 / 核验 |
| --- | --- | --- |
| Auth only | PASS | [34084056790](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084056790) |
| 最小文本 | TEST_VISIBLE | 发送 [34084493834](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084493834)；核验 [34084582522](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084582522) |
| 中等文本 | TEST_VISIBLE | 发送 [34084644548](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084644548)；核验 [34084693301](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084693301) |
| 长文本 | TEST_VISIBLE（2 段） | 发送 [34084760216](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084760216)；核验 [34084976339](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34084976339) |
| 自制测试图片 | TEST_VISIBLE | 发送 [34085032249](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34085032249)；核验 [34085092011](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34085092011) |
| 完整 locked Revision 4（文字 + 1 张美图） | TEST_VISIBLE | 发送 [34085161551](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34085161551)；核验 [34085233684](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34085233684) |

完整测试 Attempt 保存两个真实论坛 `postTaskId`（文字、图片各一个）、Revision、Hash、分段 Hash 和媒体数；仅写入 `test-publish-log/`。论坛只读 API 已确认测试帖标题可见；仍需要 QQ 客户端截图来保存图片实际渲染的前台证据。

论坛只读列表是一个有限的当前窗口，不能当作永久归档：2026-09-07 的只读复核 [34095984760](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34095984760) 当时仅返回 4 条当前帖子，未再列出较早的最小文本帖。这次调用没有发帖、没有显示鉴权材料，也没有显示权限/网络错误；它说明需要把 Action 结果、不可变测试日志和 QQ 客户端截图一并保留，而不能以晚些时候的列表缺项反推原测试失败。

已归档的 QQ 客户端前台证据：

- [完整日报文字帖（2026-09-06）](evidence/qq-runtime/2026-09-06-forum-full-report-text-visible.png)：可见测试标题与完整日报正文。这是早期文本链路的前台证据，独立 `qq-test` 目标仍以本节的 2026-09-07 API 核验为准。
- [论坛美图帖（2026-09-07）](evidence/qq-runtime/2026-09-07-forum-artwork-visible.png)：可见 `【测试】绮喵日报 V4-C report-artwork 2026-09-06` 主题及完整图片渲染；与最新独立测试目标的完整 Revision 4 图像步骤对应。
- [长文本与测试子频道（2026-09-07）](evidence/qq-runtime/2026-09-07-forum-long-text-and-test-channel-visible.png)：QQ 客户端可见 `【测试】绮喵日报 V4-C 长文本测试 2026-09-06（1/2）`、`中等长度测试` 和左上方已选的 `绮喵小课堂` 子频道；这提供了长文本前台渲染和测试目标归属的直接证据。
- [最小文本列表（2026-09-07）](evidence/qq-runtime/2026-09-07-qq-client-minimal-text-list-visible.png)：QQ 客户端在 `绮喵小课堂` 测试子频道中可见最新 `【测试】绮喵日报 V4-C 连接测试 2026-09-07` 帖子。
- [最小文本详情（2026-09-07）](evidence/qq-runtime/2026-09-07-qq-client-minimal-text-detail-visible.png)：QQ 客户端展开最新连接测试帖，显示正文、来源子频道和发布时间。
- [GitHub test-publish Summary（2026-09-06）](evidence/qq-runtime/2026-09-06-github-test-publish-summary.png)：显示测试日期、Revision、Hash、论坛目标脱敏尾号、真实 QQ 返回 ID 和 `TEST_SUBMITTED` 写回结果；它与本地前台截图及后续 `TEST_VISIBLE` API 核验互为审计证据。

## GitHub Pages 中转美图实测（2026-09-07）

- 测试运行：[34082915741](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34082915741)。
- 输入：已锁定的 2026-09-06 Revision 4，文本 1 段、已选 Pixiv 美图 1 张。
- 中转：Runner 临时下载图片，再从 GitHub Pages 临时目录提供 QQ 可访问的 HTTPS URL；本次实际图片任务 `postTaskId` 为 `1788755027343757988`。
- 结果：用户已在 QQ 客户端人工确认文字与美图均可见；测试日志已从 `TEST_SUBMITTED` 审计提升为 `TEST_PUBLISHED`。这不是 QQ 只读 API 自动核验。
- 队列：这是 qq-test 测试，不消耗正式美图确认队列。Pages 中转文件暂保留，待正式发布策略确定后再受控清理。

## 测试帖删除

此前用户要求测试完成后立即删除，清理工作流因此只匹配 `【测试】绮喵日报 V4-C`，且要求手动输入 `DELETE_TEST_POSTS`。真实删除调用曾被 QQ 拒绝：`HTTP 400 / 11264 / 频道未对机器人授权`，没有把该次失败写成删除成功。随后运行 [34017690886](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34017690886) 成功读取当前可见列表，匹配为 **0**，因此未尝试删除任何帖子；实际选图测试后的 [34018882951](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018882951) 仍收到同一错误。用户现已明确同意暂时**不以删帖能力作为发帖测试的前置条件**；删帖工作流保持手动、默认不运行，也不影响文本发帖路径。

## 尚待补齐的证据

QQ 客户端的最小文本、长文本、图片、完整日报文字及测试子频道归属截图均已归档。Pages 的 `web/data/dashboard.json` 以 `qqTest.status = TEST_VISIBLE` 投影测试发布状态；真实测试日志是 `test-publish-log/`，不会污染生产 `publish-log/`。

在这些项目完成前，不宣布 V4-C PASS，也不进入 V4-D。

## V4-C PASS Gate 审计（2026-09-07）

| 强制 Gate | 当前证据 | 结论 |
| --- | --- | --- |
| 官方 SDK、REST 单次发布与平台调研 | `V4_QQ_PLATFORM_AUDIT.md`；固定 `@tencent-connect/qqbot-nodejs@1.0.4` | PASS |
| GitHub-hosted Runner 鉴权与网络 | Auth run `34084056790`，后续真实论坛创建/读取成功 | PASS |
| 测试与生产目标分离 | 两个 Environment 均已配置；脱敏变量比较 `sameChannel=false`；工作流只读取 `qq-test` | PASS |
| 最小 / 中等 / 长文本 | `34084493834` / `34084644548` / `34084760216` 均为 `TEST_VISIBLE` | PASS |
| 确定性、按段落安全切分 | Node test `chunks deterministically at section boundaries`、`does not cut an individual report item` | PASS |
| 图片路径 | 图片 run `34085032249` 为 `TEST_VISIBLE`；完整 Revision 4 使用 Pages 中转图 | PASS（API 侧） |
| 锁定 Revision、Hash 和真实 QQ ID | 完整 run `34085161551`；`test-publish-log/2026-09-06.json` 保存 Revision 4、Hash、两个 `postTaskId` | PASS |
| 测试 / 正式发布记录隔离 | 只写 `test-publish-log/`；生产 `publish-log/` 不被 qq-test 占用 | PASS |
| 部分失败、恢复、重试 | QQ Node test 16/16 通过，其中覆盖 retry、partial failure、resume 不重复已发 chunk | PASS |
| Secrets 扫描与 Pages 状态 | GitHub validate run `34086374501` 成功；工作流测试 `secret_scan.py` | PASS |
| 生产自动真实发送关闭 | `publish.yml` 仍为 DRY RUN，`qq-test-publish.yml` 只有 `workflow_dispatch` | PASS |
| QQ 客户端前台截图归档 | 已归档最小文本、长文本、完整日报文字、美图及测试子频道归属 | PASS |

## 最小文本补发（2026-09-07）

因用户确认早期最小文本帖可能被误删，已按用户请求只向 `qq-test` 子频道补发一条最小文本；没有读取 production Environment，也没有影响生产 `publish-log/`。

- 发送：[34122331551](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34122331551)。
- 只读可见性核验：[34122454054](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34122454054)，结果为 `TEST_VISIBLE`。
- 最新 `test-publish-log/2026-09-07.json` 保存了真实论坛 `postTaskId`、26 字文本 Hash 和 `verifiedAt`；未保存 Secret 或 token。
- 待用户提供该新帖的 QQ 客户端脱敏截图后，截图证据项即可闭合。

用户随后提供了最小文本列表和详情截图，现已归档至上述 evidence 目录；该项已闭合。

## V4-C 结论

**V4-C — PASS（2026-09-07）**。QQ 官方机器人已在独立 `qq-test` 论坛子频道完成 GitHub-hosted Runner 的真实鉴权、最小/中等/长文本、图片和锁定完整 Revision 的发送与可见性验证。测试日志保存真实 QQ 返回任务 ID，测试状态与生产状态隔离，重试/部分失败恢复/Secret Scan 均通过。`publish.yml` 仍是 QQ-free DRY RUN，`qq-test` 没有 schedule；本阶段到此停止，不启用 V4-D 的正式自动发布。
