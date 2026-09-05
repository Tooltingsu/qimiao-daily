# 绮喵日报 V4

> 主架构为 GitHub Repository + GitHub Actions + GitHub Pages + QQ 官方机器人。当前 Windows Desktop V3 是保留的参考实现；真实 QQ 发布尚未接入，所有发布流程仅为 DRY_RUN。

绮喵日报以 GitHub 作为数据和审计记录中心：人工数据在 `data/`，采集结果在 `collected/`，计算结果在 `generated/`，不可变日报修订在 `reports/`，发布历史在 `publish-log/`。Pages 只读展示，编辑和运行入口跳转到 GitHub 原生页面。

## 运行与构建

本地诊断入口：

```powershell
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- validate --root .
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- calculate --root . --date 2026-09-05
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- collect --root . --date 2026-09-05
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- generate --root . --date 2026-09-05 --source-commit LOCAL
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- publish --root . --date 2026-09-05 --dry-run true
```

## Legacy Desktop V3

所有用户数据保存于 `%LOCALAPPDATA%\QimiaoDaily`：

- `data/qimiao.db`：SQLite 数据和审核状态
- `images/`：缓存的美图缩略图
- `reports/`：按日期导出的 Markdown 与 TXT 日报
- `config/`：可编辑的来源和调度配置

备份整个目录即可保存日报、审核记录、图片和配置。

## 来源配置

可在 `%LOCALAPPDATA%\QimiaoDaily\config\source_settings.json` 创建以下配置；文件缺失或格式无效时会安全回退到内置默认值。

```json
{
  "bgiRepositories": [
    "babalae/better-genshin-impact",
    "babalae/bettergi-scripts-list"
  ],
  "artwork": {
    "dailyRankingLimit": 30,
    "targetCount": 30,
    "directArtworkIds": []
  }
}
```

`directArtworkIds` 用于补充指定 Pixiv 作品；它会和每日榜候选合并、去重后进入人工审核区。Pixiv Session 在“设置”页保存，使用当前 Windows 用户的 DPAPI 加密，绝不写进数据库或日志。

生日来源配置见 `config/birthday_sources.json`，调度配置见 `config/scheduler.json`。外部来源受登录、限流或网络影响时，页面会保留失败状态，不能视为已刷新成功。

## 项目结构

```text
src/    WPF、领域、数据、采集和服务代码
tests/  自动化测试
tools/  独立的审计与 QA 工具
docs/   当前架构和配置说明
publish/ 当前单文件发布物
```

历史 Web 迁移资料和阶段性 QA 证据已移出主工程目录，不参与构建或运行。

## V4-A POC

V4 的人工数据、自动采集、计算结果、日报修订和发布日志分别位于 `data/`、`collected/`、`generated/`、`reports/` 和 `publish-log/`。跨平台入口是：

```powershell
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- validate --root .
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- calculate --root . --date 2026-09-05
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- generate --root . --date 2026-09-05 --source-commit LOCAL_POC
dotnet run --project src/QimiaoDaily.V4/QimiaoDaily.V4.csproj -- publish --root . --date 2026-09-05 --dry-run true
```

迁移边界、状态机、安全模型和本次实证结果见 `docs/v4/`，其中汇总报告为 `docs/v4/V4_POC_RESULT.md`。生产 QQ 发布在 V4-A 被程序明确禁止。
