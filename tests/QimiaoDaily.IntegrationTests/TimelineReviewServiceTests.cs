using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class TimelineReviewServiceTests
{
    [Fact]
    public async Task ConfirmAndReturn_WriteAuditHistoryAndMoveBetweenReviewColumns()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "EVENT", "test", VerificationStatus.VerifiedOfficial, null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid", "evidence", "test", DateTimeOffset.UtcNow)); database.TimelineItems.Add(item); await database.SaveChangesAsync();
        var service = new TimelineReviewService(database); await service.ConfirmAsync(item.Id, "tester", "checked", DateTimeOffset.UtcNow);
        Assert.Equal(ReviewStatus.Confirmed, (await database.TimelineItems.SingleAsync()).ReviewStatus); Assert.Single(await database.ReviewActions.Where(x => x.Action == "CONFIRM").ToListAsync());
        await service.ReturnAsync(item.Id, "tester", "recheck", DateTimeOffset.UtcNow);
        Assert.Equal(ReviewStatus.Pending, (await database.TimelineItems.SingleAsync()).ReviewStatus); Assert.Single(await database.ReviewActions.Where(x => x.Action == "RETURN").ToListAsync()); Assert.Equal(2, await database.TimelineItemRevisions.CountAsync());
    }

    [Fact]
    public async Task Archive_WritesRejectAuditAndRemovesCandidateFromReviewColumns()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "EVENT", "rejected", VerificationStatus.VerifiedOfficial, null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/rejected", "evidence", "test", DateTimeOffset.UtcNow)); database.TimelineItems.Add(item); await database.SaveChangesAsync();
        await new TimelineReviewService(database).ArchiveAsync(item.Id, "tester", "not an in-game reward event", DateTimeOffset.UtcNow);
        Assert.Equal(ReviewStatus.Archived, (await database.TimelineItems.SingleAsync()).ReviewStatus);
        Assert.Contains(await database.ReviewActions.ToListAsync(), x => x.Action == "ARCHIVE" && x.Reason.Contains("not an in-game"));
    }

    [Fact]
    public async Task RestoreArchived_WritesAuditAndReturnsItemToPendingReview()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "EVENT", "archived", VerificationStatus.Conflict, null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("official-a", "notice", "https://example.invalid/a", "evidence a", "test", DateTimeOffset.UtcNow));
        item.AddEvidence(new EvidenceRecord("official-b", "notice", "https://example.invalid/b", "evidence b", "test", DateTimeOffset.UtcNow));
        database.TimelineItems.Add(item); await database.SaveChangesAsync();
        var service = new TimelineReviewService(database);
        await service.ArchiveAsync(item.Id, "tester", "temporary archive", DateTimeOffset.UtcNow);
        await service.RestoreAsync(item.Id, "tester", "review conflict", DateTimeOffset.UtcNow);

        Assert.Equal(ReviewStatus.Pending, (await database.TimelineItems.SingleAsync()).ReviewStatus);
        Assert.Contains(await database.ReviewActions.ToListAsync(), x => x.Action == "RESTORE");
        Assert.Equal(2, await database.TimelineItemRevisions.CountAsync());
    }

    [Fact]
    public async Task Edit_WritesFieldRevisionsAndEditAction()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "EVENT", "before", VerificationStatus.VerifiedOfficial, "2026-08-15", "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        database.TimelineItems.Add(item); await database.SaveChangesAsync();
        await new TimelineReviewService(database).EditAsync(item.Id, "VIDEO", "after", VerificationStatus.VerifiedMultiSource, "2026-08-16", "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow, null, "tester", "corrected official type", DateTimeOffset.UtcNow);

        var saved = await database.TimelineItems.SingleAsync();
        Assert.Equal("VIDEO", saved.ItemType);
        Assert.Equal("after", saved.Title);
        Assert.Equal(ReviewStatus.Pending, saved.ReviewStatus);
        Assert.Contains(await database.ReviewActions.ToListAsync(), x => x.Action == "EDIT" && x.Reason == "corrected official type");
        Assert.Contains(await database.TimelineItemRevisions.ToListAsync(), x => x.FieldName == "Title" && x.OldValue == "before" && x.NewValue == "after");
    }

    [Fact]
    public async Task Edit_PersistsTimeSourceDiagnostics()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new TimelineItem("NTE", "GACHA", "残虹", VerificationStatus.Unverified,
            "版本更新后", "Asia/Shanghai", null, TimePrecision.Relative, DateTimeOffset.UtcNow,
            startTimePrecision: TimePrecision.Relative, startTimeSource: "relative-expression", startExpression: "版本更新后");
        database.TimelineItems.Add(item); await database.SaveChangesAsync();

        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        await new TimelineReviewService(database).EditAsync(item.Id, "GACHA", "残虹", VerificationStatus.VerifiedOfficial,
            "8月20日10:00", "Asia/Shanghai", start, TimePrecision.Exact, DateTimeOffset.UtcNow,
            start.AddDays(14), "tester", "corrected activity time", DateTimeOffset.UtcNow,
            startTimePrecision: TimePrecision.Exact, endTimePrecision: TimePrecision.Exact,
            startTimeSource: "activity-body", endTimeSource: "activity-body", startExpression: "8月20日10:00", endExpression: "9月3日05:59");

        var saved = await database.TimelineItems.SingleAsync();
        Assert.Equal("activity-body", saved.StartTimeSource);
        Assert.Equal(TimePrecision.Exact, saved.StartTimePrecision);
        Assert.Contains(await database.TimelineItemRevisions.ToListAsync(), x => x.FieldName == "StartTimeSource");
    }
}
