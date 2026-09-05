using QimiaoDaily.Collectors;
using QimiaoDaily.Core;

namespace QimiaoDaily.Core.Tests;

public sealed class TimelineTimeTests
{
    [Fact]
    public void RelativeStartDoesNotBecomeNormalizedVersionStart()
    {
        var candidate = GameCandidate.Relative(
            "nte-1", "NTE", "GACHA", "残虹",
            "8月13日版本更新后-9月3日05:59", "Asia/Shanghai",
            "版本更新后", "9月3日05:59", "official-body-1");

        Assert.Null(candidate.NormalizedTime);
        Assert.Equal(TimePrecision.Relative, candidate.StartTimePrecision);
        Assert.Equal("版本更新后", candidate.StartExpression);
    }

    [Fact]
    public void RemainingTimeUsesFloorDaysAndHours()
    {
        var now = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.FromHours(8));
        var text = TimeDisplay.Format(
            now.AddHours(-1),
            now.AddDays(3).AddHours(8),
            now,
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));

        Assert.Equal("剩余 3天8小时", text);
    }

    [Fact]
    public void FutureActivityUsesDistanceToStart()
    {
        var now = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.FromHours(8));
        var text = TimeDisplay.Format(
            now.AddDays(1).AddHours(6),
            now.AddDays(2),
            now,
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));

        Assert.Equal("距开始 1天6小时", text);
    }

    [Fact]
    public void UnknownEndTimeIsExplicit()
    {
        var now = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.FromHours(8));
        Assert.Equal(
            "结束时间待确认",
            TimeDisplay.Format(now.AddHours(-1), null, now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")));
    }
}
