using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class DailyReportBuilderTests
{
    [Fact]
    public async Task BuildAutomaticSections_UsesOnlyReportEligibleData()
    {
        var options=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;await using var db=new QimiaoDailyDbContext(options);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();var now=DateTimeOffset.UtcNow;var item=new TimelineItem("GENSHIN","EVENT","confirmed event",VerificationStatus.VerifiedOfficial,"2026-08-15", "Asia/Shanghai", null,TimePrecision.DateOnly,now);item.AddEvidence(new EvidenceRecord("official","notice","https://example.invalid","source","test",now));item.Confirm("tester","checked",now);db.TimelineItems.Add(item);db.GitCommitRecords.Add(new GitCommitRecord{Repository="a/b",Sha="123456789",Subject="selected commit",Url="https://github.com/a/b/commit/123456789",FetchedAt=now,SelectedForReport=true});await db.SaveChangesAsync();
        var service=new DailyReportService(db);await service.BuildAutomaticSectionsAsync(new DateOnly(2026,8,15));var draft=await service.GetOrCreateAsync(new DateOnly(2026,8,15));Assert.Contains("confirmed event",draft.Sections.Single(x=>x.Key=="games").Text);Assert.Contains("selected commit",draft.Sections.Single(x=>x.Key=="bgi").Text);
    }

    [Fact]
    public async Task BuildAutomaticSections_DoesNotReportOlderConfirmedVersionWhenLatestIsPending()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var oldFetched = DateTimeOffset.UtcNow.AddMinutes(-2);
        var old = new TimelineItem("GENSHIN", "EVENT", "旧时间活动", VerificationStatus.VerifiedOfficial, "old", "Asia/Shanghai", new DateTimeOffset(2026, 8, 15, 3, 0, 0, TimeSpan.Zero), TimePrecision.Exact, oldFetched, new DateTimeOffset(2026, 8, 16, 3, 0, 0, TimeSpan.Zero));
        old.SetCanonicalIdentity("GENSHIN:stable-1");
        old.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/old", "old", "test", oldFetched));
        old.Confirm("tester", "checked", oldFetched);
        var latest = new TimelineItem("GENSHIN", "EVENT", "新时间活动", VerificationStatus.Unverified, "new", "Asia/Shanghai", null, TimePrecision.Relative, DateTimeOffset.UtcNow, startTimePrecision: TimePrecision.Relative);
        latest.SetCanonicalIdentity("GENSHIN:stable-1");
        latest.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/new", "new", "test", DateTimeOffset.UtcNow));
        db.TimelineItems.AddRange(old, latest);
        await db.SaveChangesAsync();

        await new DailyReportService(db).BuildAutomaticSectionsAsync(new DateOnly(2026, 8, 15));
        var draft = await new DailyReportService(db).GetOrCreateAsync(new DateOnly(2026, 8, 15));

        Assert.DoesNotContain("旧时间活动", draft.Sections.Single(x => x.Key == "games").Text);
    }
}
