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

## 测试帖删除

此前用户要求测试完成后立即删除，清理工作流因此只匹配 `【测试】绮喵日报 V4-C`，且要求手动输入 `DELETE_TEST_POSTS`。真实删除调用曾被 QQ 拒绝：`HTTP 400 / 11264 / 频道未对机器人授权`，没有把该次失败写成删除成功。随后运行 [34017690886](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34017690886) 成功读取当前可见列表，匹配为 **0**，因此未尝试删除任何帖子；实际选图测试后的 [34018882951](https://github.com/Tooltingsu/qimiao-daily/actions/runs/34018882951) 仍收到同一错误。用户现已明确同意暂时**不以删帖能力作为发帖测试的前置条件**；删帖工作流保持手动、默认不运行，也不影响文本发帖路径。

## 尚待补齐的证据

1. 对一张**已确认且可合法临时下载**的实际 Pixiv 选图完成下载、校验、QQ 图片发布和清理的测试；
2. QQ 客户端中最小文本、长文本、图片、完整日报四张脱敏截图，保存到 `docs/v4/evidence/qq-runtime/`。

在这些项目完成前，不宣布 V4-C PASS，也不进入 V4-D。
