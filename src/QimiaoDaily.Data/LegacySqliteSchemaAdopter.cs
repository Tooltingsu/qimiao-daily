using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace QimiaoDaily.Data;

/// <summary>
/// One-time bridge for databases created by releases that used EnsureCreated.
/// Normal startup never creates schema with this class; it runs only before the
/// baseline migration is adopted and is intentionally idempotent.
/// </summary>
internal static class LegacySqliteSchemaAdopter
{
    private const string BaselineMigrationId = "20260815100113_Baseline";
    private const string ProductVersion = "8.0.11";

    private static readonly string[] CreateTableStatements =
    [
        "CREATE TABLE IF NOT EXISTS \"timeline_items\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_timeline_items\" PRIMARY KEY, \"GameCode\" TEXT NOT NULL, \"ItemType\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, \"ReviewStatus\" TEXT NOT NULL, \"VerificationStatus\" TEXT NOT NULL, \"SourceTime\" TEXT NULL, \"SourceTimezone\" TEXT NULL, \"NormalizedTime\" TEXT NULL, \"EndAt\" TEXT NULL, \"TimePrecision\" TEXT NOT NULL, \"FetchedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"evidence\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_evidence\" PRIMARY KEY, \"TimelineItemId\" TEXT NOT NULL, \"SourceProvider\" TEXT NOT NULL, \"SourceType\" TEXT NOT NULL, \"SourceUrl\" TEXT NOT NULL, \"PageTitle\" TEXT NULL, \"SourceText\" TEXT NOT NULL, \"PublishedAt\" TEXT NULL, \"OriginalTimezone\" TEXT NULL, \"NormalizedTime\" TEXT NULL, \"ParserVersion\" TEXT NOT NULL, \"VerificationStatus\" TEXT NOT NULL DEFAULT 'Unverified', \"FetchedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"review_actions\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_review_actions\" PRIMARY KEY, \"TimelineItemId\" TEXT NOT NULL, \"Action\" TEXT NOT NULL, \"Actor\" TEXT NOT NULL, \"Reason\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"timeline_item_revisions\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_timeline_item_revisions\" PRIMARY KEY, \"TimelineItemId\" TEXT NOT NULL, \"FieldName\" TEXT NOT NULL, \"OldValue\" TEXT NOT NULL, \"NewValue\" TEXT NOT NULL, \"Actor\" TEXT NOT NULL, \"Reason\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"legacy_import_runs\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_legacy_import_runs\" PRIMARY KEY, \"SourcePath\" TEXT NOT NULL, \"SourceHash\" TEXT NOT NULL, \"BackupPath\" TEXT NOT NULL, \"ImportedAt\" TEXT NOT NULL, \"TimelineItemsImported\" INTEGER NOT NULL, \"ArchivedRows\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"legacy_archive_records\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_legacy_archive_records\" PRIMARY KEY, \"ImportRunId\" TEXT NOT NULL, \"TableName\" TEXT NOT NULL, \"RowKey\" TEXT NOT NULL, \"PayloadJson\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"git_commit_records\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_git_commit_records\" PRIMARY KEY, \"Repository\" TEXT NOT NULL, \"Sha\" TEXT NOT NULL, \"Subject\" TEXT NOT NULL, \"Body\" TEXT NULL, \"Author\" TEXT NULL, \"AuthorDate\" TEXT NULL, \"CommitterDate\" TEXT NULL, \"PullRequestNumber\" INTEGER NULL, \"PullRequestUrl\" TEXT NULL, \"Url\" TEXT NOT NULL, \"FetchedAt\" TEXT NOT NULL, \"SelectedForReport\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"birthdays\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_birthdays\" PRIMARY KEY, \"Character\" TEXT NOT NULL, \"Franchise\" TEXT NOT NULL, \"Month\" INTEGER NOT NULL, \"Day\" INTEGER NOT NULL, \"Source\" TEXT NOT NULL, \"SourceUrl\" TEXT NOT NULL, \"Evidence\" TEXT NOT NULL, \"VerificationStatus\" TEXT NOT NULL, \"VerifiedAt\" TEXT NOT NULL, \"Enabled\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"anniversaries\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_anniversaries\" PRIMARY KEY, \"Title\" TEXT NOT NULL, \"StartedOn\" TEXT NOT NULL, \"Enabled\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"calendar_events\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_calendar_events\" PRIMARY KEY, \"EventDate\" TEXT NOT NULL, \"Kind\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, \"Detail\" TEXT NULL, \"Source\" TEXT NOT NULL, \"SourceUrl\" TEXT NULL, \"Enabled\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"endgame_cycle_rules\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_endgame_cycle_rules\" PRIMARY KEY, \"GameCode\" TEXT NOT NULL, \"CanonicalName\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"RecurrenceKind\" TEXT NOT NULL, \"IntervalDays\" INTEGER NULL, \"AnchorStart\" TEXT NULL, \"RuleVersion\" TEXT NOT NULL, \"SourceUrl\" TEXT NOT NULL, \"Evidence\" TEXT NOT NULL, \"VerificationStatus\" TEXT NOT NULL, \"Enabled\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"endgame_cycle_instances\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_endgame_cycle_instances\" PRIMARY KEY, \"RuleId\" TEXT NOT NULL, \"GameCode\" TEXT NOT NULL, \"CanonicalName\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"StartAt\" TEXT NOT NULL, \"EndAt\" TEXT NULL, \"RuleVersion\" TEXT NOT NULL, \"TimelineItemId\" TEXT NULL, \"SourceUrl\" TEXT NOT NULL, \"VerificationStatus\" TEXT NOT NULL, \"ReviewStatus\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"artworks\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_artworks\" PRIMARY KEY, \"Platform\" TEXT NOT NULL, \"ArtworkId\" TEXT NOT NULL, \"NormalizedUrl\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, \"Author\" TEXT NOT NULL, \"AuthorId\" TEXT NOT NULL, \"SourceUrl\" TEXT NOT NULL, \"ThumbnailUrl\" TEXT NOT NULL, \"ThumbnailSha256\" TEXT NULL, \"PublishedAt\" TEXT NOT NULL, \"FetchedAt\" TEXT NOT NULL, \"ReviewStatus\" TEXT NOT NULL, \"SelectedForReport\" INTEGER NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"seen_artworks\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_seen_artworks\" PRIMARY KEY, \"Platform\" TEXT NOT NULL, \"ArtworkId\" TEXT NOT NULL, \"NormalizedUrl\" TEXT NOT NULL, \"ContentSha256\" TEXT NULL, \"FirstSeenAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"report_drafts\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_report_drafts\" PRIMARY KEY, \"ReportDate\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"report_sections\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_report_sections\" PRIMARY KEY, \"ReportDraftId\" TEXT NOT NULL, \"Key\" TEXT NOT NULL, \"SortOrder\" INTEGER NOT NULL, \"Text\" TEXT NOT NULL, \"Dirty\" INTEGER NOT NULL, \"ManualOverride\" INTEGER NOT NULL, \"IsDeleted\" INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS \"provider_health_records\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_provider_health_records\" PRIMARY KEY, \"ProviderName\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"LastSuccessAt\" TEXT NULL, \"LastFailureAt\" TEXT NULL, \"LastLatencyMs\" INTEGER NOT NULL, \"ItemCount\" INTEGER NOT NULL, \"ParserStatus\" TEXT NOT NULL, \"FailureCount\" INTEGER NOT NULL, \"LastError\" TEXT NULL, \"UpdatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"scheduler_task_records\" (\"Id\" TEXT NOT NULL CONSTRAINT \"PK_scheduler_task_records\" PRIMARY KEY, \"TaskKey\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"ScheduleText\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"LastRunAt\" TEXT NULL, \"NextRunAt\" TEXT NULL, \"FailureCount\" INTEGER NOT NULL, \"MaxRetries\" INTEGER NOT NULL, \"LastError\" TEXT NULL, \"UpdatedAt\" TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)"
    ];

    private static readonly (string Table, string Column, string Definition)[] Columns =
    [
        ("timeline_items", "EndAt", "TEXT NULL"),
        ("evidence", "PageTitle", "TEXT NULL"), ("evidence", "PublishedAt", "TEXT NULL"), ("evidence", "OriginalTimezone", "TEXT NULL"), ("evidence", "NormalizedTime", "TEXT NULL"), ("evidence", "VerificationStatus", "TEXT NOT NULL DEFAULT 'Unverified'"),
        ("git_commit_records", "PullRequestNumber", "INTEGER NULL"), ("git_commit_records", "PullRequestUrl", "TEXT NULL"),
        ("artworks", "ThumbnailSha256", "TEXT NULL"), ("report_sections", "IsDeleted", "INTEGER NOT NULL DEFAULT 0")
    ];

    private static readonly string[] IndexStatements =
    [
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_legacy_import_runs_SourceHash\" ON \"legacy_import_runs\" (\"SourceHash\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_legacy_archive_records_ImportRunId_TableName_RowKey\" ON \"legacy_archive_records\" (\"ImportRunId\", \"TableName\", \"RowKey\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_git_commit_records_Repository_Sha\" ON \"git_commit_records\" (\"Repository\", \"Sha\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_birthdays_Character_Franchise\" ON \"birthdays\" (\"Character\", \"Franchise\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_calendar_events_EventDate_Title\" ON \"calendar_events\" (\"EventDate\", \"Title\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_endgame_cycle_rules_GameCode_CanonicalName_RuleVersion\" ON \"endgame_cycle_rules\" (\"GameCode\", \"CanonicalName\", \"RuleVersion\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_endgame_cycle_instances_TimelineItemId\" ON \"endgame_cycle_instances\" (\"TimelineItemId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_endgame_cycle_instances_GameCode_CanonicalName_StartAt\" ON \"endgame_cycle_instances\" (\"GameCode\", \"CanonicalName\", \"StartAt\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_artworks_Platform_ArtworkId\" ON \"artworks\" (\"Platform\", \"ArtworkId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_artworks_NormalizedUrl\" ON \"artworks\" (\"NormalizedUrl\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_seen_artworks_Platform_ArtworkId\" ON \"seen_artworks\" (\"Platform\", \"ArtworkId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_seen_artworks_NormalizedUrl\" ON \"seen_artworks\" (\"NormalizedUrl\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_seen_artworks_ContentSha256\" ON \"seen_artworks\" (\"ContentSha256\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_report_drafts_ReportDate\" ON \"report_drafts\" (\"ReportDate\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_report_sections_ReportDraftId_Key\" ON \"report_sections\" (\"ReportDraftId\", \"Key\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_provider_health_records_ProviderName\" ON \"provider_health_records\" (\"ProviderName\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_scheduler_task_records_TaskKey\" ON \"scheduler_task_records\" (\"TaskKey\")"
    ];

    public static void Adopt(QimiaoDailyDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var sql in CreateTableStatements) Execute(connection, transaction, sql);

        foreach (var (table, column, definition) in Columns)
        {
            if (!HasColumn(connection, transaction, table, column))
                Execute(connection, transaction, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}");
        }

        foreach (var sql in IndexStatements) Execute(connection, transaction, sql);
        Execute(connection, transaction, $"INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{BaselineMigrationId}', '{ProductVersion}')");
        transaction.Commit();
    }

    private static bool HasColumn(DbConnection connection, DbTransaction transaction, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void Execute(DbConnection connection, DbTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
