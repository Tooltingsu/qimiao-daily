using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.Localization;

public static class BirthdayRefreshSummaryFormatter
{
    public static string Format(BirthdayRefreshReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Games.Count == 0) return "生日刷新结果：没有可用覆盖数据。";

        var details = report.Games.Select(game =>
            $"{DisplayNameMapper.Game(game.Franchise)} 已知{game.Known}，未知{game.Unknown}，待审核{game.Pending}");
        var warning = report.FailedSourceCount > 0 ? "部分来源失败，" : string.Empty;
        return "生日刷新结果：" + string.Join("；", details) + $"。{warning}未知日期不会自动启用。";
    }
}
