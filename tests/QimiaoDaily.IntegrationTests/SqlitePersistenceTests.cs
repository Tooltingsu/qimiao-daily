using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task TimelineItem_WithEvidence_IsPersistedAndLoaded()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new QimiaoDailyDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        var item = new TimelineItem("Genshin", "notice", "测试事项", VerificationStatus.VerifiedMultiSource,
            "2026-08-14 10:00", "Asia/Shanghai", DateTimeOffset.UtcNow,
            TimePrecision.Exact, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("官方公告", "notice", "https://example.invalid/source", "可验证的公告正文", "phase-b", DateTimeOffset.UtcNow));
        context.TimelineItems.Add(item);
        await context.SaveChangesAsync();

        var restored = await context.TimelineItems.Include(x => x.Evidence).SingleAsync();

        Assert.Single(restored.Evidence);
        Assert.Equal("测试事项", restored.Title);
    }
}
