using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors.Tests;

public sealed class AnnouncementTimeParserTests
{
    [Fact]
    public void GenshinBodyActivityTimeWinsOverListVersionTime()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "活动时间：2026年8月18日 10:00 - 2026年8月28日 03:59",
            "2026-08-13 06:00",
            "2026-08-28 03:59",
            "GENSHIN");

        Assert.Equal(new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(8)), parsed.Start);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 3, 59, 0, TimeSpan.FromHours(8)), parsed.End);
        Assert.Equal("activity-body", parsed.StartSource);
    }

    [Fact]
    public void RelativeVersionExpressionRemainsRelative()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "开放时间：4.4版本更新后-2026/08/25 15:00",
            "2026/07/15 06:00",
            "",
            "STARRAIL");

        Assert.Null(parsed.Start);
        Assert.Equal(TimePrecision.Relative, parsed.Precision);
        Assert.Equal("4.4版本更新后", parsed.StartExpression);
        Assert.Equal(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.FromHours(8)), parsed.End);
    }

    [Fact]
    public void LabeledBodyTimeIsPreferredWhenListTimeDiffers()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "祈愿时间\n2026/08/21 18:00-2026/09/01 18:00",
            "2026-08-13 06:00",
            "2026-09-01 18:00",
            "GENSHIN");

        Assert.Equal(21, parsed.Start!.Value.Day);
        Assert.Equal(18, parsed.Start.Value.Hour);
        Assert.Equal("activity-body", parsed.StartSource);
    }

    [Fact]
    public void ExplicitLabeledRangeWinsOverUnrelatedVersionRelativePhrase()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "活动时间：<t class=\"t_lc\">2026/08/10 04:00</t> ~ <t class=\"t_lc\">2026/11/02 03:59</t>。本活动在7.0版本更新后开放。",
            "",
            "",
            "GENSHIN");

        Assert.Equal(new DateTimeOffset(2026, 8, 10, 4, 0, 0, TimeSpan.FromHours(8)), parsed.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 3, 59, 0, TimeSpan.FromHours(8)), parsed.End);
        Assert.Equal("activity-body", parsed.StartSource);
        Assert.Equal("activity-body", parsed.EndSource);
    }

    [Fact]
    public void ListStartIsNotUsedWhenBodyHasNoActivityTime()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "活动说明：完成任务即可获得奖励。",
            "2026-08-13 06:00",
            "2026-08-28 03:59",
            "GENSHIN");

        Assert.Null(parsed.Start);
        Assert.Equal(TimePrecision.Relative, parsed.Precision);
        Assert.Equal("2026-08-28 03:59", parsed.EndExpression);
    }

    [Fact]
    public void SentinelFutureEndDateIsNotStoredAsAnActivityEnd()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "长期服务说明",
            "2026-07-15 06:00",
            "2036-07-15 14:00",
            "STARRAIL");

        Assert.Null(parsed.End);
        Assert.Equal(TimePrecision.Relative, parsed.Precision);
        Assert.Equal("2036-07-15 14:00", parsed.EndExpression);
    }

    [Fact]
    public void SentinelFutureDateInLabeledBodyIsNotStored()
    {
        var parsed = AnnouncementTimeParser.ParseForTest(
            "活动时间：2026/08/01 10:00 - 2038/01/01 07:00",
            "2026-07-15 06:00",
            "2038-01-01 07:00",
            "STARRAIL");

        Assert.Equal(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.FromHours(8)), parsed.Start);
        Assert.Null(parsed.End);
        Assert.Equal("2038/01/01 07:00", parsed.EndExpression);
    }
}
