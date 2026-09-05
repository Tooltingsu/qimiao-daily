using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class LegacySqliteImportTests
{
    [Fact]
    public async Task ImportAsync_MapsTimeline_ArchivesRows_AndIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiaodaily-import-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var legacy = Path.Combine(root, "legacy.db");
        await using (var source = new SqliteConnection($"Data Source={legacy}"))
        {
            await source.OpenAsync();
            await Execute(source, "CREATE TABLE games(id INTEGER PRIMARY KEY, code TEXT); INSERT INTO games VALUES(1,'GENSHIN'); CREATE TABLE timeline_items(id INTEGER PRIMARY KEY, game_id INTEGER, item_type TEXT, title TEXT, verification_status TEXT, source_time TEXT, source_timezone TEXT, normalized_time TEXT, time_precision TEXT, fetched_at TEXT, review_status TEXT); INSERT INTO timeline_items VALUES(7,1,'EVENT','真实迁移测试','VERIFIED_OFFICIAL','2026-08-14 10:00','Asia/Shanghai','2026-08-14T10:00:00+08:00','EXACT','2026-08-14T02:00:00Z','CONFIRMED'); CREATE TABLE evidence(id INTEGER PRIMARY KEY, timeline_item_id INTEGER, source_provider TEXT, source_type TEXT, source_url TEXT, excerpt TEXT, parser_version TEXT, fetched_at TEXT); INSERT INTO evidence VALUES(1,7,'official','notice','https://example.invalid','原始公告','test','2026-08-14T02:00:00Z'); CREATE TABLE notes(id INTEGER PRIMARY KEY, value TEXT); INSERT INTO notes VALUES(1,'must survive');");
        }
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={Path.Combine(root, "target.db")}").Options;
        await using var target = new QimiaoDailyDbContext(options);
        var importer = new LegacySqliteImportService(target, new QimiaoDailyPaths(Path.Combine(root, "appdata")));

        var first = await importer.ImportAsync(legacy);
        var second = await importer.ImportAsync(legacy);

        Assert.False(first.AlreadyImported); Assert.True(File.Exists(first.BackupPath)); Assert.Equal(1, first.TimelineItemsImported);
        Assert.True(second.AlreadyImported);
        Assert.Equal(1, await target.TimelineItems.CountAsync());
        Assert.Equal(1, await target.Evidence.CountAsync());
        Assert.Equal(4, await target.LegacyArchiveRecords.CountAsync());
    }

    private static async Task Execute(SqliteConnection connection, string sql) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
}
