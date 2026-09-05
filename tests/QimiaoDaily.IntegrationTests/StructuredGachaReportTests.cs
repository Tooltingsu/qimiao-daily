using QimiaoDaily.Core;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class StructuredGachaReportTests
{
    [Fact]
    public void ConfirmedVerifiedStructuredGacha_UsesLocalizedPoolLabel()
    {
        var now = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);
        var item = new TimelineItem("GENSHIN", "GACHA", "测试角色", VerificationStatus.VerifiedOfficial,
            "2026-08-18 10:00:00", "Asia/Shanghai", now.AddHours(10), TimePrecision.Exact, now,
            gachaPoolKind: "CHARACTER", gachaPoolPhase: "FIRST_HALF");
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/gacha", "source", "test", now));
        item.Confirm("tester", "verified", now);

        var report = DailyReportFormatter.FormatGames([item], new DateOnly(2026, 8, 18), now);

        Assert.Contains("\u4e0a\u534a\u5361\u6c60", report);
        Assert.Contains("\u6d4b\u8bd5\u89d2\u8272", report);
    }

    [Fact]
    public void UnknownStructuredGacha_FallsBackToExistingPoolTitle()
    {
        var now = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);
        var item = new TimelineItem("GENSHIN", "GACHA", "原始标题", VerificationStatus.VerifiedOfficial,
            "2026-08-18 10:00:00", "Asia/Shanghai", now.AddHours(10), TimePrecision.Exact, now);
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/gacha", "source", "test", now));
        item.Confirm("tester", "verified", now);

        var report = DailyReportFormatter.FormatGames([item], new DateOnly(2026, 8, 18), now);

        Assert.Contains("\u5361\u6c60\u300c\u539f\u59cb\u6807\u9898\u300d", report);
    }
}
