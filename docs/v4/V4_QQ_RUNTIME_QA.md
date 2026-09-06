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

## 美图的当前 Gate

测试图片已通过，但当前 locked revision 的已选 Pixiv 美图只有作品页 URL、没有可下载/可上传的直接图像 URL。因此它不能被悄悄替换成普通文本或测试图：正式 Publisher 将其视为 `PUBLISH_MEDIA_FAILED`，除非在临时下载、格式/大小校验及 QQ 图片发送后全部成功。仓库不会保存 Pixiv 原图。

## 测试帖删除

用户要求测试完成后立即删除。清理工作流只匹配 `【测试】绮喵日报 V4-C`，且要求手动输入 `DELETE_TEST_POSTS`。2026-09-06 的真实删除调用被 QQ 拒绝：`HTTP 400 / 11264 / 频道未对机器人授权`；没有任何删除成功的证据。请先人工删除测试帖，或给机器人授予该论坛删帖权限后再运行清理工作流。

## 尚待补齐的证据

1. 用户在 QQ 客户端删除测试帖后的截图，或授予删帖权限后的清理成功日志；
2. 对一张**已确认且可合法临时下载**的实际 Pixiv 选图完成下载、校验、QQ 图片发布和清理的测试；
3. QQ 客户端中最小文本、长文本、图片、完整日报四张脱敏截图，保存到 `docs/v4/evidence/qq-runtime/`。

在这些项目完成前，不宣布 V4-C PASS，也不进入 V4-D。
