using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class DailyReportFormatterTests
{
    [Fact]
    public void DateLine_UsesChineseDateAndWeekday()
        => Assert.Equal("今天是2026年8月15日，星期六", DailyReportFormatter.DateLine(new DateOnly(2026, 8, 15)));

    [Fact]
    public void FormatCalendar_UsesDailySentencesAndChineseGameNames()
    {
        var report = DailyReportFormatter.FormatCalendar(
        [
            new CalendarOccurrence("BIRTHDAY", "胡桃", new DateOnly(2026, 8, 10), "GENSHIN"),
            new CalendarOccurrence("BIRTHDAY", "薄荷", new DateOnly(2026, 8, 10), "NTE"),
            new CalendarOccurrence("BIRTHDAY", "琪亚娜", new DateOnly(2026, 8, 10), "HI3"),
            new CalendarOccurrence("ANNIVERSARY", "原神", new DateOnly(2026, 8, 10), "6周年"),
            new CalendarOccurrence("FESTIVAL", "端午", new DateOnly(2026, 8, 10)),
            new CalendarOccurrence("SOLAR_TERM", "立秋", new DateOnly(2026, 8, 10))
        ]);

        Assert.Equal(string.Join(Environment.NewLine,
        [
            "今天是二十四节气 立秋",
            "今天是节日 端午",
            "今天是【原神 胡桃】的生日",
            "今天是【异环 薄荷】的生日",
            "今天是【崩坏三 琪亚娜】的生日",
            "今天是【原神】6周年纪念日"
        ]), report);
    }

    [Fact]
    public void FormatGames_UsesRealNodesAndFiltersUnconfirmedItems()
    {
        var now = new DateTimeOffset(2026, 8, 15, 2, 0, 0, TimeSpan.Zero);
        var eventItem = Item("GENSHIN", "EVENT", "测试活动", new DateTimeOffset(2026, 8, 15, 3, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 15, 19, 59, 0, TimeSpan.Zero));
        var video = Item("NTE", "VIDEO", "官方视频", new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero));
        var pending = new TimelineItem("GENSHIN", "EVENT", "不可进入日报", VerificationStatus.VerifiedOfficial, null, null, null, TimePrecision.DateOnly, now);
        eventItem.Confirm("test", "verified", now);
        video.Confirm("test", "verified", now);

        var report = DailyReportFormatter.FormatGames([eventItem, video, pending], new DateOnly(2026, 8, 15), now);

        Assert.Contains("-原神 活动「测试活动」今日11:00开始", report);
        Assert.Contains("剩余 0天17小时，将于明日03:59结束", report);
        Assert.Contains("-异环 发布视频【官方视频】", report);
        Assert.DoesNotContain("不可进入日报", report);
    }

    [Fact]
    public void FormatBgi_SeparatesBothRepositories()
    {
        var now = DateTimeOffset.UtcNow;
        var commits = new[]
        {
            new GitCommitRecord { Repository = "babalae/better-genshin-impact", Sha = "abcdef123456", Subject = "本体更新", Url = "https://example.invalid/a", FetchedAt = now, SelectedForReport = true },
            new GitCommitRecord { Repository = "babalae/bettergi-scripts-list", Sha = "123456789abc", Subject = "脚本更新", Url = "https://example.invalid/b", FetchedAt = now, SelectedForReport = true }
        };

        var report = DailyReportFormatter.FormatBgi(commits);

        Assert.Contains("BGI更新预告", report);
        Assert.Contains("-本体更新 (abcdef1)", report);
        Assert.Contains("BGI仓库更新", report);
        Assert.Contains("-脚本更新 (1234567)", report);
    }

    [Fact]
    public void UnknownSectionAndCodesUseChineseFallbackLabels()
    {
        Assert.Equal("其他日报内容", DailyReportFormatter.SectionTitle("unexpected_section"));
        var item = Item("UNKNOWN_GAME", "UNKNOWN_TYPE", "未知内容", new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero));
        item.Confirm("test", "verified", DateTimeOffset.UtcNow);
        Assert.Contains("其他游戏", DailyReportFormatter.FormatGames(
            [item],
            new DateOnly(2026, 8, 15),
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void FormatV3Games_OnlyRemindsDateOnlyCalendarActivityAtStartOrEnd()
    {
        var item = new TimelineItem("GENSHIN", "EVENT", "雪原探索", VerificationStatus.VerifiedMultiSource,
            "2026-08-12", "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow,
            endAt: null, startTimePrecision: TimePrecision.DateOnly, endTimePrecision: TimePrecision.DateOnly,
            startExpression: "2026-08-12", endExpression: "2026-11-03");
        item.AddEvidence(new EvidenceRecord("calendar-image", "manual", "file:///calendar-images-import.json", "原神｜2026-08-12 至 2026-11-03", "calendar-image-v1", DateTimeOffset.UtcNow));
        item.SetDataProvenance(DataOrigin.Imported, true);
        item.Confirm("tester", "用户已确认图片日历活动", DateTimeOffset.UtcNow);

        var report = DailyReportFormatter.FormatV3Games([], [], [], [item], new DateOnly(2026, 8, 19), DateTimeOffset.UtcNow);

        Assert.DoesNotContain("雪原探索", report);
        Assert.DoesNotContain("04:00", report);
        Assert.DoesNotContain("10:00", report);

        var startReport = DailyReportFormatter.FormatV3Games([], [], [], [item], new DateOnly(2026, 8, 12), DateTimeOffset.UtcNow);
        Assert.Contains("-原神 活动「雪原探索」今日开始", startReport);

        var endReport = DailyReportFormatter.FormatV3Games([], [], [], [item], new DateOnly(2026, 11, 3), DateTimeOffset.UtcNow);
        Assert.Contains("-原神 活动「雪原探索」将于今日结束", endReport);
    }

    private static TimelineItem Item(string game, string type, string title, DateTimeOffset start, DateTimeOffset? end = null)
    {
        var item = new TimelineItem(game, type, title, VerificationStatus.VerifiedOfficial, start.ToString("O"), "UTC", start, TimePrecision.Exact, DateTimeOffset.UtcNow, end);
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/" + title, title, "test", DateTimeOffset.UtcNow));
        return item;
    }
}
