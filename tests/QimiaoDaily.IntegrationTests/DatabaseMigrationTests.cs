using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task EnsureReadyAsync_MigratesEmptyDatabase_AndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);

        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);
        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);

        Assert.Contains("20260815100113_Baseline", await database.Database.GetAppliedMigrationsAsync());
        Assert.Contains("20260819072056_ManualDataPivot", await database.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        Assert.True(await database.TimelineItems.CountAsync() == 0);
        Assert.Empty(await database.ManualEvents.ToListAsync());
        Assert.Equal(System.Data.ConnectionState.Open, database.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task EnsureReadyAsync_AdoptsEnsureCreatedStyleDatabase_WithoutDroppingRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiaodaily-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "legacy-created.db");
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, "CREATE TABLE timeline_items (Id TEXT PRIMARY KEY NOT NULL, GameCode TEXT NOT NULL, ItemType TEXT NOT NULL, Title TEXT NOT NULL, ReviewStatus TEXT NOT NULL, VerificationStatus TEXT NOT NULL, SourceTime TEXT NULL, SourceTimezone TEXT NULL, NormalizedTime TEXT NULL, TimePrecision TEXT NOT NULL, FetchedAt TEXT NOT NULL)");
            await ExecuteAsync(connection, "INSERT INTO timeline_items (Id, GameCode, ItemType, Title, ReviewStatus, VerificationStatus, SourceTime, SourceTimezone, NormalizedTime, TimePrecision, FetchedAt) VALUES ('00000000-0000-0000-0000-000000000007', 'GENSHIN', 'EVENT', '迁移保留数据', 'Pending', 'VerifiedOfficial', '2026-08-15 10:00', 'Asia/Shanghai', '2026-08-15T02:00:00+00:00', 'Exact', '2026-08-15T02:00:00+00:00')");
        }

        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);
        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);
        await QimiaoDatabaseInitializer.EnsureReadyAsync(database);

        var item = await database.TimelineItems.SingleAsync();
        Assert.Equal("迁移保留数据", item.Title);
        Assert.Null(item.EndAt);
        Assert.Contains("20260815100113_Baseline", await database.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        Assert.True(File.Exists(path));
        await database.DisposeAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
