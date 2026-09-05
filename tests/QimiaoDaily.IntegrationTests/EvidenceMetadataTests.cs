using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class EvidenceMetadataTests
{
    [Fact]
    public async Task Evidence_PersistsTraceMetadataAndVerification()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "EVENT", "metadata", VerificationStatus.VerifiedOfficial, null, "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/source", "evidence", "parser-v1", DateTimeOffset.UtcNow, "Official page", DateTimeOffset.UtcNow.AddMinutes(-1), "Asia/Shanghai", DateTimeOffset.UtcNow, VerificationStatus.VerifiedOfficial));
        db.TimelineItems.Add(item);
        await db.SaveChangesAsync();

        var restored = await db.TimelineItems.Include(x => x.Evidence).SingleAsync();
        Assert.Equal("Official page", restored.Evidence[0].PageTitle);
        Assert.Equal(VerificationStatus.VerifiedOfficial, restored.Evidence[0].VerificationStatus);
    }
}
