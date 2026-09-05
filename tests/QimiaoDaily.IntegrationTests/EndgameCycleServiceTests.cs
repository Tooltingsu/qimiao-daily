using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class EndgameCycleServiceTests
{
    [Fact]
    public async Task UpsertFromCandidateAsync_PersistsVersionedRuleAndPendingInstance()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var fetched = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(2026, 8, 16, 4, 0, 0, TimeSpan.FromHours(8));
        var candidate = new GameCandidate("spiral-1", "GENSHIN", "ENDGAME", "深境螺旋 第一期", start.ToString("O"), "Asia/Shanghai", start,
            [new CollectedEvidence("GenshinOfficial", "announcement", "https://example.invalid/spiral-1", "official cycle", fetched)], start.AddDays(14));
        var item = new TimelineItem(candidate.GameCode, candidate.ItemType, candidate.Title, VerificationStatus.VerifiedOfficial,
            candidate.SourceTime, candidate.SourceTimezone, candidate.NormalizedTime, TimePrecision.Exact, fetched, candidate.EndAt);
        database.TimelineItems.Add(item);
        await new EndgameCycleService(database).UpsertFromCandidateAsync(candidate, item.Id);
        await database.SaveChangesAsync();

        var rule = await database.EndgameCycleRules.SingleAsync();
        var instance = await database.EndgameCycleInstances.SingleAsync();
        Assert.Equal("GENSHIN_SPIRAL_ABYSS", rule.CanonicalName);
        Assert.Equal("official-announcement-v1", rule.RuleVersion);
        Assert.Equal("ANNOUNCEMENT_BACKED", rule.RecurrenceKind);
        Assert.Null(rule.IntervalDays);
        Assert.Equal(item.Id, instance.TimelineItemId);
        Assert.Equal(ReviewStatus.Pending, instance.ReviewStatus);
        Assert.Equal(rule.RuleVersion, instance.RuleVersion);
        Assert.Equal("https://example.invalid/spiral-1", instance.SourceUrl);
    }

    [Fact]
    public async Task UpsertFromCandidateAsync_DoesNotInventRuleForNonEndgame()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var candidate = new GameCandidate("event-1", "GENSHIN", "EVENT", "official event", null, "Asia/Shanghai", null,
            [new CollectedEvidence("GenshinOfficial", "announcement", "https://example.invalid/event-1", "official", DateTimeOffset.UtcNow)]);
        Assert.False(await new EndgameCycleService(database).UpsertFromCandidateAsync(candidate, Guid.NewGuid()));
        Assert.Empty(await database.EndgameCycleRules.ToListAsync());
        Assert.Empty(await database.EndgameCycleInstances.ToListAsync());
    }
}
