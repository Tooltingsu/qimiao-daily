using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.Localization;

public static class GameRefreshSummaryFormatter
{
    public static string Format(GameRefreshReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Games.Count == 0) return "游戏刷新结果：没有返回任何游戏结果。";

        var details = report.Games.Select(game =>
        {
            var coverage = $"{Math.Round(game.CoverageRatio * 100d):0}%";
            var warning = game.Warnings.Count == 0 ? string.Empty : "，可能漏采";
            return $"{DisplayNameMapper.Game(game.GameCode)} 发现{game.CandidateCount}条，解析{game.ParsedCount}条，新增{game.NewCount}，更新{game.UpdatedCount}，冲突{game.ConflictCount}，覆盖率{coverage}{warning}";
        });
        return "游戏刷新结果：" + string.Join("；", details) + "。候选仍需人工审核。";
    }
}
