using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class ManualDataServiceTests
{
    [Fact]
    public async Task CreateEventAsync_WritesConfirmedManualRecordWithoutEvidence()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ManualDataService(database);
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));

        var saved = await service.CreateEventAsync(new ManualEventInput("GENSHIN", "映夏", start, start.AddDays(7), ""));

        Assert.Equal(DataOrigin.Manual, saved.Origin);
        Assert.True(saved.UserConfirmed);
        Assert.Empty(database.Evidence);
    }

    [Fact]
    public async Task UpdateArchiveAndBannerCrud_PersistsChangesAndOrderedCharacters()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ManualDataService(database);
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        var activity = await service.CreateEventAsync(new ManualEventInput("GENSHIN", "映夏", start, start.AddDays(7), ""));

        var updated = await service.UpdateEventAsync(activity.Id, new ManualEventInput("GENSHIN", "新映夏", start, start.AddDays(8), "备注"));
        await service.ArchiveEventAsync(activity.Id);
        var banner = await service.CreateBannerAsync(new BannerInput("GENSHIN", "月之一", "上半卡池", null, start, start.AddDays(21), "", ["哥伦比娅", "雷电将军"]));

        Assert.Equal("新映夏", updated.Name);
        Assert.True((await database.ManualEvents.SingleAsync()).Archived);
        Assert.Equal(["哥伦比娅", "雷电将军"], banner.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name));
    }

    [Fact]
    public async Task SaveVersionAsync_ReportsOverlapUnlessForced()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ManualDataService(database);
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        await service.SaveVersionAsync(new GameVersionInput("GENSHIN", "6.0", "月之一", start, start.AddDays(42), ""), false);

        var rejected = await service.SaveVersionAsync(new GameVersionInput("GENSHIN", "6.1", "月之二", start.AddDays(30), start.AddDays(72), ""), false);
        var forced = await service.SaveVersionAsync(new GameVersionInput("GENSHIN", "6.1", "月之二", start.AddDays(30), start.AddDays(72), ""), true);

        Assert.True(rejected.HasOverlapWarning);
        Assert.Null(rejected.Version);
        Assert.False(forced.HasOverlapWarning);
        Assert.NotNull(forced.Version);
    }

    [Fact]
    public async Task BannerAndVersion_UpdateArchiveAndDelete_PreserveFormalDataLifecycle()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ManualDataService(database);
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        var banner = await service.CreateBannerAsync(new BannerInput("GENSHIN", "A", "UP", null, start, start.AddDays(14), "", ["甲"]));
        var version = (await service.SaveVersionAsync(new GameVersionInput("GENSHIN", "6.8", "A", start, start.AddDays(42), ""), false)).Version!;

        await service.UpdateBannerAsync(banner.Id, new BannerInput("GENSHIN", "B", "UP", null, start, start.AddDays(14), "", ["乙", "甲"]));
        await service.ArchiveBannerAsync(banner.Id);
        await service.UpdateVersionAsync(version.Id, new GameVersionInput("GENSHIN", "6.8", "B", start, start.AddDays(42), ""), true);
        await service.ArchiveVersionAsync(version.Id);
        await service.DeleteBannerAsync(banner.Id);
        await service.DeleteVersionAsync(version.Id);

        Assert.Empty(database.Banners);
        Assert.Empty(database.GameVersions);
        Assert.Contains(database.ManualDataAudits, x => x.Action == "DELETE");
    }

    [Fact]
    public async Task RefreshVersionDependentRulesAsync_RebuildsOnlyVersionRulesFromManualVersions()
    {
        await using var database = await CreateDatabaseAsync();
        await new V3DataMigrationService(database).ApplyAsync();
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        await new ManualDataService(database).SaveVersionAsync(
            new GameVersionInput("GENSHIN", "7.0", "测试版本", start, start.AddDays(42), ""), false);

        var refreshed = await new EndgameScheduleMaintenanceService(database)
            .RefreshVersionDependentRulesAsync(new DateOnly(2026, 8, 20));

        Assert.Equal(3, refreshed);
        var genshin = await database.EndgameOccurrences.ToListAsync();
        var stygianId = database.EndgameRules.Single(r => r.RuleKey == "GENSHIN_STYGIAN_ONSLAUGHT").Id;
        var frenziedId = database.EndgameRules.Single(r => r.RuleKey == "GENSHIN_FRENZIED_ONSLAUGHT").Id;
        var arbitrationId = database.EndgameRules.Single(r => r.RuleKey == "STARRAIL_SECTOR_ARBITRATION").Id;
        Assert.Contains(genshin, x => x.RuleId == stygianId && x.StartAt.Date == new DateTime(2026, 8, 27));
        Assert.Contains(genshin, x => x.RuleId == frenziedId && x.StartAt.Date == new DateTime(2026, 8, 27));
        Assert.DoesNotContain(genshin, x => x.RuleId == arbitrationId);
    }

    private static async Task<QimiaoDailyDbContext> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        return database;
    }
}
