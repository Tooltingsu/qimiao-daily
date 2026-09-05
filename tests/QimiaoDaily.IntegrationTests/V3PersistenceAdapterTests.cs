using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class V3PersistenceAdapterTests
{
    [Fact]
    public async Task EndgameStore_PersistsDateOnlyAnchorOverridesAndReplacesCurrentPlusNextTwo()
    {
        await using var database = await CreateDatabaseAsync();
        var engine = new EndgameScheduleEngine();
        var rule = engine.Reanchor(EndgameScheduleRules.OuterRealm, new DateOnly(2026, 8, 28));
        rule = engine.WithOverride(rule, new EndgameOccurrenceOverride(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 12), Notes: "维护顺延"));

        await engine.RefreshAsync(rule, new DateOnly(2026, 8, 29), new DbContextEndgameScheduleStore(database));

        var persistedRule = await database.EndgameRules.SingleAsync();
        var anchor = await database.EndgameAnchors.SingleAsync();
        var occurrences = await database.EndgameOccurrences.OrderBy(x => x.Sequence).ToListAsync();
        Assert.Equal("NTE_OUTER_REALM", persistedRule.RuleKey);
        Assert.Equal(new DateOnly(2026, 8, 28), anchor.AnchorDate);
        Assert.Equal("DATE_ONLY", persistedRule.TimePrecision);
        Assert.Null(persistedRule.StartTime);
        Assert.Equal(3, occurrences.Count);
        Assert.All(occurrences, x => Assert.Equal("DATE_ONLY", x.TimePrecision));
        Assert.All(occurrences, x => Assert.Null(x.StartTime));
        Assert.Equal([new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 26)], occurrences.Select(x => x.OccurrenceDate));
        Assert.Equal("维护顺延", occurrences[1].Notes);
        Assert.True(occurrences[1].IsOverride);

        await engine.RefreshAsync(rule, new DateOnly(2026, 9, 13), new DbContextEndgameScheduleStore(database));

        Assert.Equal(3, await database.EndgameOccurrences.CountAsync());
    }

    [Fact]
    public async Task ImportStore_PreviewDoesNotWrite_AndConfirmWritesFormalImportedRecordsWithAudit()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new QimiaoImportService(new DbContextQimiaoImportStore(database));
        const string json = """
            {
              "schemaVersion": 1,
              "events": [{ "id": "event-1", "game": "GENSHIN", "name": "夏日活动", "startAt": "2026-08-20T10:00:00+08:00", "endAt": "2026-09-01T03:59:00+08:00", "notes": "活动备注" }],
              "banners": [{ "id": "banner-1", "game": "GENSHIN", "name": "角色祈愿", "type": "上半卡池", "characters": ["角色甲", "角色乙"], "startAt": "2026-08-20T10:00:00+08:00", "endAt": "2026-09-10T03:59:00+08:00" }],
              "versions": [{ "id": "version-1", "game": "GENSHIN", "versionNumber": "6.8", "versionName": "新版本", "startAt": "2026-08-20T10:00:00+08:00", "endAt": "2026-10-01T03:59:00+08:00" }],
              "birthdays": [{ "id": "birthday-1", "game": "GENSHIN", "character": "托马", "month": 1, "day": 9 }],
              "anniversaries": [{ "id": "anniversary-1", "title": "项目纪念日", "startedOn": "2020-08-20" }]
            }
        """;

        var preview = await service.PreviewAsync(json);
        Assert.Equal(5, preview.Entries.Count);
        Assert.Empty(await database.ManualEvents.ToListAsync());
        Assert.Empty(await database.ImportRecords.ToListAsync());

        await service.ConfirmAsync(preview, preview.Entries.Select(x => x.SelectionKey));

        var eventRecord = await database.ManualEvents.SingleAsync();
        var banner = await database.Banners.Include(x => x.Characters).SingleAsync();
        var version = await database.GameVersions.SingleAsync();
        var birthday = await database.Birthdays.SingleAsync();
        var anniversary = await database.Anniversaries.SingleAsync();
        Assert.Equal(DataOrigin.Imported, eventRecord.Origin);
        Assert.True(eventRecord.UserConfirmed);
        Assert.Equal(DataOrigin.Imported, banner.Origin);
        Assert.True(banner.UserConfirmed);
        Assert.Equal(["角色甲", "角色乙"], banner.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name));
        Assert.Equal(DataOrigin.Imported, version.Origin);
        Assert.True(version.UserConfirmed);
        Assert.Equal(DataOrigin.Imported, birthday.DataOrigin);
        Assert.True(birthday.UserConfirmed);
        Assert.Equal("GENSHIN", birthday.Franchise);
        Assert.True(anniversary.Enabled);
        Assert.Equal(DataOrigin.Imported, anniversary.DataOrigin);
        Assert.True(anniversary.UserConfirmed);
        Assert.Equal(5, await database.ImportRecords.CountAsync());
        Assert.Equal(5, await database.ManualDataAudits.CountAsync());
    }

    [Fact]
    public async Task ImportStore_ConfirmsDateOnlyCalendarEventsWithoutInventingTime()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new QimiaoImportService(new DbContextQimiaoImportStore(database));
        const string json = """
            { "schemaVersion": 1, "events": [], "banners": [], "versions": [], "birthdays": [], "anniversaries": [],
              "calendarEvents": [{ "id": "nte-activity-1", "date": "2026-08-13", "title": "单骑破浪", "kind": "GAME", "detail": "异环｜2026-08-13 至 2026-09-24｜图片日历" }] }
            """;

        var preview = await service.PreviewAsync(json);
        await service.ConfirmAsync(preview, preview.Entries.Select(x => x.SelectionKey));

        var item = await database.CalendarEvents.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 13), item.EventDate);
        Assert.Equal("单骑破浪", item.Title);
        Assert.Equal("GAME", item.Kind);
        Assert.Equal("qimiao-import.json", item.Source);
        Assert.Contains("2026-09-24", item.Detail);
        Assert.Single(await database.ImportRecords.Where(x => x.RecordType == "calendarEvent").ToListAsync());
        Assert.Single(await database.ManualDataAudits.Where(x => x.EntityType == "calendarEvent").ToListAsync());
    }

    [Fact]
    public async Task ImportStore_PromotesGameCalendarEventToConfirmedManualEvent()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new QimiaoImportService(new DbContextQimiaoImportStore(database));
        const string json = """
            { "schemaVersion": 1, "events": [], "banners": [], "versions": [], "birthdays": [], "anniversaries": [],
              "calendarEvents": [{ "id": "genshin-activity-1", "date": "2026-08-12", "title": "雪原探索", "kind": "GAME", "detail": "原神｜2026-08-12 至 2026-11-03｜图片日历" }] }
            """;

        var preview = await service.PreviewAsync(json);
        await service.ConfirmAsync(preview, preview.Entries.Select(x => x.SelectionKey));

        var item = await database.ManualEvents.SingleAsync();
        Assert.Equal("GENSHIN", item.Game);
        Assert.Equal("雪原探索", item.Name);
        Assert.Equal(new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.FromHours(8)), item.StartAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.FromHours(8)), item.EndAt);
        Assert.Equal(DataOrigin.Imported, item.Origin);
        Assert.True(item.UserConfirmed);
        Assert.Empty(await database.TimelineItems.ToListAsync());
    }

    [Fact]
    public async Task ImportStore_UpdatesExistingBirthdayByExactAliasWhenChineseNameReplacesLegacyName()
    {
        await using var database = await CreateDatabaseAsync();
        var existing = new BirthdayEntity
        {
            Character = "Thoma", CanonicalCharacterNameZhCn = "Thoma", Aliases = "Thoma", Franchise = "GENSHIN",
            Month = 1, Day = 9, Source = "legacy", SourceTier = "legacy", VerificationStatus = VerificationStatus.Unverified,
            SourceUrl = string.Empty, Evidence = string.Empty, VerifiedAt = DateTimeOffset.UtcNow, Enabled = true
        };
        database.Birthdays.Add(existing);
        await database.SaveChangesAsync();
        var service = new QimiaoImportService(new DbContextQimiaoImportStore(database));
        const string json = "{ \"schemaVersion\": 1, \"events\": [], \"banners\": [], \"versions\": [], \"birthdays\": [{ \"id\": \"genshin-thoma\", \"game\": \"GENSHIN\", \"character\": \"托马\", \"aliases\": \"Thoma\", \"month\": 1, \"day\": 9 }], \"anniversaries\": [] }";

        var preview = await service.PreviewAsync(json);
        await service.ConfirmAsync(preview, preview.Entries.Select(x => x.SelectionKey));

        Assert.Equal(1, await database.Birthdays.CountAsync(x => x.Franchise == "GENSHIN" && x.Character == "托马"));
        Assert.Equal(0, await database.Birthdays.CountAsync(x => x.Franchise == "GENSHIN" && x.Character == "Thoma"));
        var restored = await database.Birthdays.SingleAsync(x => x.Franchise == "GENSHIN" && x.Character == "托马");
        Assert.Equal("Thoma", restored.Aliases);
        Assert.Equal(DataOrigin.Imported, restored.DataOrigin);
        Assert.True(restored.UserConfirmed);
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
