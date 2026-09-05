using QimiaoDaily.Desktop.Localization;
using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.Tests;

public sealed class GameRefreshSummaryFormatterTests
{
    [Fact]
    public void Format_UsesChineseGameNamesAndReportsCoverageChanges()
    {
        var report = new GameRefreshReport([
            GameCoverageResult.FromCounts("GENSHIN", 12, 12, newCount: 5, updatedCount: 2, conflictCount: 1),
            GameCoverageResult.FromCounts("STARRAIL", 8, 7, newCount: 3, updatedCount: 1),
            GameCoverageResult.FromCounts("NTE", 4, 4, newCount: 0, updatedCount: 2, conflictCount: 1)
        ]);

        var text = GameRefreshSummaryFormatter.Format(report);

        Assert.Contains("原神", text);
        Assert.Contains("崩坏：星穹铁道", text);
        Assert.Contains("异环", text);
        Assert.Contains("发现12条", text);
        Assert.Contains("新增5", text);
        Assert.Contains("更新2", text);
        Assert.Contains("冲突1", text);
        Assert.Contains("覆盖率100%", text);
        Assert.Contains("覆盖率88%", text);
        Assert.DoesNotContain("GENSHIN", text);
        Assert.DoesNotContain("STARRAIL", text);
        Assert.DoesNotContain("NTE", text);
    }
}
