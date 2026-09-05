using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class V3DataMigrationServiceTests
{
    [Fact]
    public async Task EnsureReadyAsync_AppliesTheProvenanceCorrectionToAnExistingTemporaryDatabase()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);
        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);
        database.TimelineItems.Add(Item("EVENT", "legacy event"));
        database.Birthdays.Add(new BirthdayEntity { Character = "派蒙", Franchise = "GENSHIN", Month = 6, Day = 1, Source = "legacy", Enabled = true });
        await database.SaveChangesAsync();

        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);

        Assert.Equal(DataOrigin.LegacyAuto, (await database.TimelineItems.SingleAsync()).DataOrigin);
        Assert.Equal(DataOrigin.Imported, (await database.Birthdays.SingleAsync()).DataOrigin);
    }

    [Fact]
    public async Task ApplyAsync_LabelsLegacyEventAndGachaWithoutPromotingThemToManual_AndInitializesBirthdaysAsImported()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();

        database.TimelineItems.AddRange(
            Item("EVENT", "old event"),
            Item("GACHA", "old gacha"),
            Item("VIDEO", "official video"));
        database.Birthdays.Add(new BirthdayEntity
        {
            Character = "旅行者",
            Franchise = "GENSHIN",
            Month = 1,
            Day = 1,
            Source = "legacy official source",
            SourceUrl = "https://example.invalid/birthday",
            Evidence = "legacy evidence",
            VerificationStatus = VerificationStatus.VerifiedOfficial,
            VerifiedAt = DateTimeOffset.UtcNow,
            Enabled = true
        });
        await database.SaveChangesAsync();

        var result = await new V3DataMigrationService(database).ApplyAsync();
        var records = await database.TimelineItems.OrderBy(x => x.ItemType).ToListAsync();
        var birthday = await database.Birthdays.SingleAsync();

        Assert.Equal(2, result.LegacyBusinessTimelineItems);
        Assert.Equal(DataOrigin.LegacyAuto, records.Single(x => x.ItemType == "EVENT").DataOrigin);
        Assert.Equal(DataOrigin.LegacyAuto, records.Single(x => x.ItemType == "GACHA").DataOrigin);
        Assert.All(records.Where(x => x.ItemType is "EVENT" or "GACHA"), x => Assert.False(x.UserConfirmed));
        Assert.Equal(DataOrigin.AutoCollected, records.Single(x => x.ItemType == "VIDEO").DataOrigin);
        Assert.Equal(DataOrigin.Imported, birthday.DataOrigin);
        Assert.True(birthday.UserConfirmed);
        Assert.Contains("legacy official source", birthday.OriginTrace, StringComparison.Ordinal);
        Assert.Equal(1, result.BirthdaysInitialized);
        Assert.Equal(9, result.EndgameRulesSeeded);
        Assert.Equal(9, await database.EndgameRules.CountAsync());
    }

    [Fact]
    public async Task ApplyAsync_DoesNotDowngradeAlreadyFormalManualEvent()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var item = Item("EVENT", "already manual");
        item.SetDataProvenance(DataOrigin.Manual, true);
        database.TimelineItems.Add(item);
        await database.SaveChangesAsync();

        var result = await new V3DataMigrationService(database).ApplyAsync();

        var saved = await database.TimelineItems.SingleAsync();
        Assert.Equal(DataOrigin.Manual, saved.DataOrigin);
        Assert.True(saved.UserConfirmed);
        Assert.Equal(0, result.LegacyBusinessTimelineItems);
    }

    private static TimelineItem Item(string type, string title)
    {
        var item = new TimelineItem("GENSHIN", type, title, VerificationStatus.VerifiedOfficial,
            null, "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow);
        item.SetCanonicalIdentity($"GENSHIN:{type}:{title}");
        return item;
    }
}
