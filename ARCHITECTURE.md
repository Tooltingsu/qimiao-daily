# 绮喵日报架构

## 运行链路

```text
WPF UI -> ViewModel -> Application Services -> Core review rules
                                  -> EF Core SQLite / HttpClient Providers
```

v2 是原生 Windows 桌面程序，不启动 Web 后端、Node、Python、浏览器或 localhost 服务。

## 项目分层

- `QimiaoDaily.Core`：时间、审核状态、证据、提醒和日报门禁。
- `QimiaoDaily.Data`：EF Core SQLite、迁移、数据模型和本地路径。
- `QimiaoDaily.Collectors`：游戏、视频、BGI、生日和美图 Provider。
- `QimiaoDaily.Services`：采集刷新、审核、归档、日报、来源健康和调度。
- `QimiaoDaily.Desktop`：WPF Shell、导航、页面状态和用户操作。

## 数据门禁

```text
Provider -> Fetch / Parse -> Normalize / Validate -> Evidence -> PENDING
                                                    -> 人工 CONFIRMED -> 日报
```

未核验、冲突、退回、待审核和已归档内容不能进入正式日报。手工修改日报段落由 `Dirty` / `ManualOverride` 保留。

## 数据库迁移

新结构使用 EF Core migrations。启动时执行 `Database.Migrate()`；仅针对早期 `EnsureCreated` 数据库执行一次兼容接入，不删除或覆盖用户数据。
