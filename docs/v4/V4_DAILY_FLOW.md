# 每日自动流程（Asia/Shanghai）

- **19:05**：`Collect V4 automatic sources` 采集 BGI、官方视频和美图元数据。视频日报窗口固定为**昨日 18:00（含）至今日 18:00（不含）**。
- 采集写回 `main` 后，`Generate V4 report revision` 自动计算并生成今日最新 `READY` revision。
- **20:05、20:10、20:15、20:20**：`QQ daily automatic release (text + artwork)` 作为发布看门狗运行。GitHub 定时任务可能延后，所以不能假定精确到 20:00。
- 发布任务优先使用已经人工锁定的 revision；没有人工锁定时，锁定当时最新有效 revision，并记录 `LOCKED_AUTO / AUTO_DEADLINE`。
- 仅将锁定 revision 的确认美图下载至 GitHub Pages 临时中转目录，验证可访问后以**文字和图片同一篇论坛帖**提交。
- 同一日期已有真实提交时，普通自动任务会被幂等保护拒绝；修正版只能走带明确 `force` 的手动生产发布工作流。

QQ 的论坛接口返回 `task_id` 代表提交已受理；发布记录会保留为 `SUBMITTED_VISIBILITY_PENDING`，便于在 QQ 客户端核验可见性。
