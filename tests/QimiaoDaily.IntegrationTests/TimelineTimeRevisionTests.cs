using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class TimelineTimeRevisionTests
{
    [Fact]
    public async Task TimeCorrectionCreatesNewPendingSnapshotAndRevision()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();

        var fetched = DateTimeOffset.UtcNow.AddMinutes(-2);
        var old = new TimelineItem("NTE", "GACHA", "残虹", VerificationStatus.Unverified,
            "8月13日版本更新后-9月3日05:59", "Asia/Shanghai", null, TimePrecision.Relative, fetched,
            startTimePrecision: TimePrecision.Relative, endTimePrecision: TimePrecision.Exact,
            startTimeSource: "relative-expression", startExpression: "版本更新后");
        old.SetCanonicalIdentity("NTE:nte-1");
        old.AddEvidence(new EvidenceRecord("NteOfficialWebsite", "notice", "https://example.invalid/nte-1", "relative", "old", fetched));
        database.TimelineItems.Add(old);
        await database.SaveChangesAsync();

        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        var end = new DateTimeOffset(2026, 9, 3, 5, 59, 0, TimeSpan.FromHours(8));
        var candidate = new GameCandidate("nte-1", "NTE", "GACHA", "残虹",
            "8月20日10:00-9月3日05:59", "Asia/Shanghai", start,
            [new CollectedEvidence("NteOfficialWebsite", "notice", "https://example.invalid/nte-1", "exact", DateTimeOffset.UtcNow, NormalizedTime: start)], end,
            TimePrecision.Exact, TimePrecision.Exact, "activity-body", "activity-body", "8月20日10:00", "9月3日05:59", "nte-1:start", "nte-1:end");

        var result = await new TimelineCandidateImportService(database).ApplyCandidateAsync(candidate, "test-parser-v2", "test", "time correction");

        Assert.Equal(TimelineChangeKind.TimeChanged, result.ChangeKind);
        var latest = (await database.TimelineItems.ToListAsync()).OrderByDescending(x => x.FetchedAt).First();
        Assert.Equal(ReviewStatus.Pending, latest.ReviewStatus);
        Assert.Equal(start, latest.NormalizedTime);
        Assert.Equal("activity-body", latest.StartTimeSource);
        Assert.Contains(await database.TimelineItemRevisions.ToListAsync(), x => x.FieldName == "NormalizedTime" && x.OldValue == string.Empty);
        Assert.Contains(await database.TimelineItemRevisions.ToListAsync(), x => x.FieldName == "StartTimeSource");
    }
}
