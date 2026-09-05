# 数据源与证据政策

## 优先级

1. P0：游戏官方站点、官方社区、官方视频/社交账号。
2. P1：官方社区镜像或官方转载。
3. P2：可信 Wiki 或数据站，仅作补充或交叉验证。
4. P3：搜索仅用于发现页面，搜索摘要不能作为证据。

## 验证状态

- `VERIFIED_OFFICIAL`：明确的官方证据。
- `VERIFIED_MULTI_SOURCE`：至少两项独立可靠来源一致。
- `UNVERIFIED`：证据不足或时间/内容不清楚。
- `CONFLICT`：可信来源相互矛盾，UI 必须同时展示证据。

每个候选项必须保存 `source_provider`、`source_type`、`source_url`、`page_title`、证据文本、`published_at`、`fetched_at`、原始时区、标准化时间、解析器版本和验证状态。解析失败必须记录失败，不得返回空列表并标记成功。
