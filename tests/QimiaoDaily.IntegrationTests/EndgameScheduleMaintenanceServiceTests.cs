using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class EndgameScheduleMaintenanceServiceTests
{
    [Fact]
    public async Task ReanchorAsync_UpdatesOnlyRequestedDateOnlyRuleAndRegeneratesCurrentPlusNextTwo()
    {
        await using var database = await CreateDatabaseAsync();
        var engine = new EndgameScheduleEngine();
        var store = new DbContextEndgameScheduleStore(database);
        await engine.RefreshAsync(EndgameScheduleRules.OuterRealm, new DateOnly(2026, 8, 22), store);
        await engine.RefreshAsync(EndgameScheduleRules.StarRailMemoryOfChaos, new DateOnly(2026, 8, 22), store);
        var outerRealmId = await database.EndgameRules.Where(x => x.RuleKey == "NTE_OUTER_REALM").Select(x => x.Id).SingleAsync();
        var otherRule = await database.EndgameRules.Where(x => x.RuleKey == "STARRAIL_MEMORY_OF_CHAOS").Select(x => x.Id).SingleAsync();

        var result = await new EndgameScheduleMaintenanceService(database).ReanchorAsync(outerRealmId, new DateOnly(2026, 8, 28), new DateOnly(2026, 8, 29));

        var anchor = await database.EndgameAnchors.SingleAsync(x => x.RuleId == outerRealmId);
        var changed = await database.EndgameOccurrences.Where(x => x.RuleId == outerRealmId).OrderBy(x => x.Sequence).ToListAsync();
        Assert.Equal([new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 25)], result.Select(x => x.StartsOn));
        Assert.Equal(new DateOnly(2026, 8, 28), anchor.AnchorDate);
        Assert.All(changed, x => { Assert.Equal("DATE_ONLY", x.TimePrecision); Assert.Null(x.StartTime); });
        Assert.Equal(3, await database.EndgameOccurrences.CountAsync(x => x.RuleId == otherRule));
    }

    [Fact]
    public async Task OverrideAsync_PersistsOneDateOnlyOverrideWithoutChangingOtherRule()
    {
        await using var database = await CreateDatabaseAsync();
        var engine = new EndgameScheduleEngine();
        var store = new DbContextEndgameScheduleStore(database);
        await engine.RefreshAsync(EndgameScheduleRules.OuterRealm, new DateOnly(2026, 8, 22), store);
        await engine.RefreshAsync(EndgameScheduleRules.StarRailMemoryOfChaos, new DateOnly(2026, 8, 22), store);
        var outerRealmId = await database.EndgameRules.Where(x => x.RuleKey == "NTE_OUTER_REALM").Select(x => x.Id).SingleAsync();
        var otherRule = await database.EndgameRules.Where(x => x.RuleKey == "STARRAIL_MEMORY_OF_CHAOS").Select(x => x.Id).SingleAsync();

        await new EndgameScheduleMaintenanceService(database).OverrideAsync(outerRealmId,
            new EndgameOccurrenceOverride(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 5), Notes: "维护顺延"), new DateOnly(2026, 8, 22));

        var changed = await database.EndgameOccurrences.Where(x => x.RuleId == outerRealmId).OrderBy(x => x.Sequence).ToListAsync();
        var storedRule = await database.EndgameRules.SingleAsync(x => x.Id == outerRealmId);
        Assert.Equal(new DateOnly(2026, 9, 4), changed[1].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 9, 5), changed[1].OccurrenceDate);
        Assert.True(changed[1].IsOverride);
        Assert.Equal("维护顺延", changed[1].Notes);
        Assert.All(changed, x => Assert.Null(x.StartTime));
        Assert.Contains("2026-09-04", storedRule.ConfigurationJson, StringComparison.Ordinal);
        Assert.Equal(3, await database.EndgameOccurrences.CountAsync(x => x.RuleId == otherRule));
    }

    private static async Task<QimiaoDailyDbContext> CreateDatabaseAsync()
    {
        var database = new QimiaoDailyDbContext(new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        return database;
    }
}
