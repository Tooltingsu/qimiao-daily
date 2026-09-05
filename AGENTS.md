# 绮喵日报工程约定

- 产品架构为 WPF + .NET 8 + EF Core SQLite；新增数据库结构必须使用 EF Core migration，禁止用 `EnsureCreated` 绕过迁移。
- 全部业务时间以 `Asia/Shanghai` 展示；保留来源时间、来源时区、标准化时间、精度和抓取时间。
- 正式日报只能读取 `CONFIRMED` 数据。采集结果必须先以候选状态保存，并保留来源证据。
- Provider 只负责抓取、解析、标准化、校验和写候选；页面不应耦合 HTML 解析逻辑。
- 密钥、Cookie、Token 和代理凭据只能来自系统安全存储或环境变量，不能写入 SQLite、日志或仓库。
- 所有可变来源、仓库和采集策略应配置在 `%LOCALAPPDATA%\QimiaoDaily\config`，不得把用户可变值写死在业务逻辑中。
- 修改必须有匹配的自动化验证。桌面交互改动至少运行桌面测试和 Release 构建。
