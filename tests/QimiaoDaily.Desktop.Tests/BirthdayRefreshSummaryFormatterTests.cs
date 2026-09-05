using QimiaoDaily.Desktop.Localization;
using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.Tests;

public sealed class BirthdayRefreshSummaryFormatterTests
{
    [Fact]
    public void Format_ReportsKnownUnknownAndPendingForAllGames()
    {
        var report = new BirthdayRefreshReport(
        [
            new BirthdayCoverageResult("GENSHIN", 51, 47, 4, 4),
            new BirthdayCoverageResult("HI3", 30, 0, 30, 30),
            new BirthdayCoverageResult("NTE", 12, 0, 12, 12)
        ], 0);

        var text = BirthdayRefreshSummaryFormatter.Format(report);

        Assert.Contains("原神 已知47，未知4，待审核4", text);
        Assert.Contains("崩坏3 已知0，未知30，待审核30", text);
        Assert.Contains("异环 已知0，未知12，待审核12", text);
        Assert.Contains("未知日期不会自动启用", text);
    }

    [Fact]
    public void Format_NotesPartialSourceFailure()
    {
        var report = new BirthdayRefreshReport([new BirthdayCoverageResult("GENSHIN", 1, 1, 0, 0)], 2);

        Assert.Contains("部分来源失败", BirthdayRefreshSummaryFormatter.Format(report));
    }
}
