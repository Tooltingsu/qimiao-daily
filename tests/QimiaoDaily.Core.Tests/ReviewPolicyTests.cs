using QimiaoDaily.Core;

namespace QimiaoDaily.Core.Tests;

public sealed class ReviewPolicyTests
{
    [Fact]
    public void Confirm_WithoutEvidence_Throws()
    {
        var item = CreateItem();

        var action = () => item.Confirm("reviewer", "evidence checked", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Confirm_WithOfficialEvidence_MakesItemReportEligible()
    {
        var item = CreateItem();
        item.AddEvidence(new EvidenceRecord("官方公告", "notice", "https://example.invalid/official", "可验证的官方公告正文", "phase-b", DateTimeOffset.UtcNow));

        item.Confirm("reviewer", "official source verified", DateTimeOffset.UtcNow);

        Assert.Equal(ReviewStatus.Confirmed, item.ReviewStatus);
        Assert.True(ReportEligibility.CanInclude(item));
    }

    [Fact]
    public void Edit_ChangesFieldsAndReturnsConfirmedItemToPending()
    {
        var item = CreateItem();
        item.AddEvidence(new EvidenceRecord("官方公告", "notice", "https://example.invalid/official", "可验证的官方公告正文", "phase-b", DateTimeOffset.UtcNow));
        item.Confirm("reviewer", "official source verified", DateTimeOffset.UtcNow);

        var changes = item.Edit("GACHA", "编辑后的标题", VerificationStatus.VerifiedMultiSource, "2026-08-15", "Asia/Shanghai", DateTimeOffset.UtcNow, TimePrecision.Exact, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(ReviewStatus.Pending, item.ReviewStatus);
        Assert.Equal("编辑后的标题", item.Title);
        Assert.Equal("GACHA", item.ItemType);
        Assert.Contains(changes, x => x.FieldName == "Title");
        Assert.Contains(changes, x => x.FieldName == "ReviewStatus" && x.NewValue == "Pending");
        Assert.False(ReportEligibility.CanInclude(item));
    }

    [Fact]
    public void Edit_RejectsEndBeforeStart()
    {
        var item = CreateItem();
        Assert.Throws<ArgumentException>(() => item.Edit("EVENT", "标题", VerificationStatus.VerifiedOfficial, null, null, DateTimeOffset.UtcNow, TimePrecision.Exact, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1)));
    }

    private static TimelineItem CreateItem() => new(
        "Genshin", "notice", "测试事项", VerificationStatus.VerifiedOfficial,
        "2026-08-14 10:00", "Asia/Shanghai", DateTimeOffset.UtcNow,
        TimePrecision.Exact, DateTimeOffset.UtcNow);
}
