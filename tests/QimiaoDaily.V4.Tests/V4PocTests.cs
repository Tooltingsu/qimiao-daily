using QimiaoDaily.V4.Core;
using QimiaoDaily.V4.Generator;
using QimiaoDaily.V4.Publishing;

namespace QimiaoDaily.V4.Tests;

public sealed class V4PocTests
{
    [Fact]
    public void GeneratorBuildsRevisionFromJsonAndLockPreventsPublishDrift()
    {
        using var fixture = new RepositoryFixture();
        var generator = new V4ReportGenerator(fixture.Repository);
        var first = generator.Generate(fixture.Date, "commit-a", fixture.Now);
        var publisher = new V4PublishService(fixture.Repository);
        var locked = publisher.Lock(fixture.Date, true, fixture.Now.AddMinutes(1));
        fixture.Repository.Write(new List<ManualEventRecord>
        {
            new("second", "GENSHIN", "锁定后新增活动", fixture.Now, fixture.Now.AddDays(1), "", true)
        }, "data", "activities.json");
        var second = generator.Generate(fixture.Date, "commit-b", fixture.Now.AddMinutes(2));
        var attempt = publisher.PublishDryRun(fixture.Date, "run-1", fixture.Now.AddMinutes(3));

        Assert.Equal(1, first.Revision);
        Assert.Equal(1, locked.Revision);
        Assert.Equal(2, second.Revision);
        Assert.Equal(first.ReportHash, attempt.ReportHash);
        Assert.DoesNotContain("锁定后新增活动", locked.Content);
        var manifest = fixture.Repository.Read<ReportManifest>("reports", fixture.Date.ToString("yyyy-MM-dd"), "manifest.json");
        Assert.Equal(first.ReportHash, manifest.ReportHash);
        Assert.Equal("commit-a", manifest.SourceCommit);
        Assert.Equal(ReportState.DryRunSucceeded, manifest.State);
        Assert.Null(manifest.PublishedAt);
        Assert.Null(attempt.PublishedAt);
    }

    [Fact]
    public void OrdinaryPublishIsIdempotentButRepublishCreatesRevisionTwo()
    {
        using var fixture = new RepositoryFixture();
        var generator = new V4ReportGenerator(fixture.Repository);
        generator.Generate(fixture.Date, "commit-a", fixture.Now);
        var publisher = new V4PublishService(fixture.Repository);
        publisher.PublishDryRun(fixture.Date, "run-1", fixture.Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => publisher.PublishDryRun(fixture.Date, "run-2", fixture.Now.AddMinutes(2)));
        var revision = publisher.PrepareRepublication(fixture.Date, "commit-b", fixture.Now.AddMinutes(3), "修正内容");
        publisher.Lock(fixture.Date, true, fixture.Now.AddMinutes(4));
        var attempt = publisher.PublishDryRun(fixture.Date, "run-3", fixture.Now.AddMinutes(5), force: true, reason: "修正内容");

        Assert.Equal(2, revision.Revision);
        Assert.Equal(2, attempt.Revision);
        Assert.Equal(2, fixture.Repository.Read<PublishLog>("publish-log", fixture.Date.ToString("yyyy-MM-dd") + ".json").Attempts.Count);
    }

    [Fact]
    public void WatchdogUsesShanghaiWindowInsteadOfAssumingCronIsExact()
    {
        var guard = new PublishWindowGuard(new V4Settings { PublishTime = "18:30" });
        Assert.False(guard.Evaluate(new DateTimeOffset(2026, 9, 5, 10, 29, 0, TimeSpan.Zero)).ShouldPublish);
        Assert.True(guard.Evaluate(new DateTimeOffset(2026, 9, 5, 10, 35, 0, TimeSpan.Zero)).ShouldPublish);
        Assert.False(guard.Evaluate(new DateTimeOffset(2026, 9, 5, 11, 1, 0, TimeSpan.Zero)).ShouldPublish);
    }

    [Fact]
    public void BgiWindowUsesTheExactShanghaiHalfOpenDailyRange()
    {
        var (start, end) = ShanghaiClock.BgiWindow(new DateOnly(2026, 9, 5));
        Assert.Equal(new DateTimeOffset(2026, 9, 4, 18, 0, 0, TimeSpan.FromHours(8)), start);
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 18, 0, 0, TimeSpan.FromHours(8)), end);
    }

    [Fact]
    public void CalendarCalculationIncludesTraditionalFestivalsSolarTermsAndManualEvents()
    {
        using var fixture = new RepositoryFixture();
        fixture.Repository.Write(new List<ManualCalendarEventRecord>
        {
            new("memorial", new DateOnly(2026, 9, 5), "MEMORIAL", "测试纪念日", "", "MANUAL", "", true)
        }, "data", "calendar-events.json");

        var records = new V4Calculator(fixture.Repository).CalculateCalendar(2026);

        Assert.Contains(records, x => x.Kind == "SOLAR_TERM");
        Assert.Contains(records, x => x.Kind == "FESTIVAL");
        Assert.Contains(records, x => x.Kind == "MEMORIAL" && x.Title == "测试纪念日");
    }

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "qimiao-v4-" + Guid.NewGuid().ToString("N"));
        public V4Repository Repository { get; }
        public DateOnly Date { get; } = new(2026, 9, 5);
        public DateTimeOffset Now { get; } = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

        public RepositoryFixture()
        {
            Repository = new V4Repository(_root);
            Repository.Write(new V4Settings(), "data", "settings.json");
            Repository.Write(new List<ManualEventRecord>(), "data", "activities.json");
            Repository.Write(new List<BannerRecord>(), "data", "banners.json");
            Repository.Write(new List<VersionRecord>(), "data", "versions.json");
            Repository.Write(new List<BirthdayRecord>(), "data", "birthdays.json");
            Repository.Write(new List<AnniversaryRecord>(), "data", "anniversaries.json");
            Repository.Write(new List<ManualCalendarEventRecord>(), "data", "calendar-events.json");
            Repository.Write(new List<CalendarRecord>(), "generated", "calendar.json");
            Repository.Write(new List<CalculatedEndgameRecord>(), "generated", "endgame.json");
            Repository.Write(new List<VideoRecord>(), "collected", "videos.json");
            Repository.Write(new List<BgiCommitRecord>(), "collected", "bgi-main.json");
            Repository.Write(new List<BgiCommitRecord>(), "collected", "bgi-scripts.json");
            Repository.Write(new List<ArtworkRecord>(), "collected", "artwork.json");
            Repository.Write(new List<ProviderStatusRecord>(), "collected", "provider-status.json");
        }

        public void Dispose() => Directory.Delete(_root, true);
    }
}
