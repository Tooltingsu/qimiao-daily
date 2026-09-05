# 来源与采集配置

所有用户可变的来源配置都在 `%LOCALAPPDATA%\QimiaoDaily\config`，修改后无需重新编译。

## `source_settings.json`

- `bgiRepositories`：需要检查的 GitHub `owner/repository` 列表。第一个显示为主要仓库，第二个显示为扩展仓库；其余仓库仍会采集并进入 BGI 总列表。
- `artwork.dailyRankingLimit`：每日从 Pixiv 榜单读取的最大候选数，范围 1–100。
- `artwork.targetCount`：每日新候选目标数，范围 1–100。
- `artwork.directArtworkIds`：可选的 Pixiv 作品 ID。它们与榜单合并并按平台和作品 ID 去重。

错误的 JSON、无效仓库名或非数字作品 ID 不会中断任务：对应项会被忽略，缺失字段使用安全默认值。

## 其他配置

- `scheduler.json`：任务执行频率与每日时间，时区为 `Asia/Shanghai`。
- `birthday_sources.json`：官方 HoYoWiki 生日入口与游戏系列。

不要将 Cookie、Token 或 API Key 放入这些 JSON 文件；Pixiv Session 只能通过桌面端“设置”页面保存。
