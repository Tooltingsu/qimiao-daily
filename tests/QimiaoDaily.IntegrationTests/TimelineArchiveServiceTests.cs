using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class TimelineArchiveServiceTests
{
    [Fact]
    public async Task ArchiveExpiredAsync_ArchivesOnlyItemsPastEndPlusThreeDays()
    {
        var options=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db=new QimiaoDailyDbContext(options);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();var now=new DateTimeOffset(2026,8,14,12,0,0,TimeSpan.Zero);
        db.TimelineItems.Add(Item(now.AddDays(-4)));db.TimelineItems.Add(Item(now.AddDays(-2)));await db.SaveChangesAsync();
        Assert.Equal(1,await new TimelineArchiveService(db).ArchiveExpiredAsync(now));
        Assert.Single(await db.TimelineItems.Where(x=>x.ReviewStatus==ReviewStatus.Archived).ToListAsync());Assert.Single(await db.ReviewActions.Where(x=>x.Action=="ARCHIVE").ToListAsync());
    }
    private static TimelineItem Item(DateTimeOffset end)=>new("GENSHIN","EVENT","test",VerificationStatus.VerifiedOfficial,null,null,null,TimePrecision.DateOnly,DateTimeOffset.UtcNow,end);
}
