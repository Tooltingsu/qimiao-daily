using Microsoft.EntityFrameworkCore;

namespace QimiaoDaily.Data;

public static class QimiaoDatabaseInitializer
{
    private const string BaselineMigrationId = "20260815100113_Baseline";

    public static void EnsureReady(QimiaoDailyDbContext database)
        => EnsureReadyAsync(database).GetAwaiter().GetResult();

    public static async Task EnsureReadyAsync(QimiaoDailyDbContext database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var connection = database.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        var keepOpen = wasOpen || IsInMemory(connection);
        if (!wasOpen) await connection.OpenAsync(cancellationToken);
        try
        {
            var hasUserTables = await HasUserTablesAsync(connection, cancellationToken);
            var hasAppliedMigration = await HasAppliedMigrationAsync(connection, cancellationToken);
            if (hasUserTables && !hasAppliedMigration)
            {
                // Older releases created schema without migration history. Adopt it and mark only
                // the baseline as applied so future changes remain migration-driven.
                LegacySqliteSchemaAdopter.Adopt(database);
            }

            await database.Database.MigrateAsync(cancellationToken);
            await new V3DataMigrationService(database).ApplyAsync(cancellationToken);
        }
        finally
        {
            if (!keepOpen) await connection.CloseAsync();
        }
    }

    private static bool IsInMemory(System.Data.Common.DbConnection connection)
        => connection.ConnectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
            || connection.ConnectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> HasUserTablesAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory')";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    private static async Task<bool> HasAppliedMigrationAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory')";
        if (Convert.ToInt32(await tableCommand.ExecuteScalarAsync(cancellationToken)) == 0) return false;

        await using var rowCommand = connection.CreateCommand();
        rowCommand.CommandText = $"SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{BaselineMigrationId}')";
        return Convert.ToInt32(await rowCommand.ExecuteScalarAsync(cancellationToken)) != 0;
    }
}
