using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;

namespace QimiaoDaily.Data;

public sealed class LegacySqliteImportService(QimiaoDailyDbContext target, QimiaoDailyPaths paths)
{
    public async Task<LegacyImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(legacyDatabasePath)) throw new FileNotFoundException("未找到遗留 SQLite 数据库。", legacyDatabasePath);
        paths.EnsureDirectories();
        await QimiaoDatabaseInitializer.EnsureReadyAsync(target, cancellationToken);
        var hash = await HashAsync(legacyDatabasePath, cancellationToken);
        var previous = await target.LegacyImportRuns.SingleOrDefaultAsync(x => x.SourceHash == hash, cancellationToken);
        if (previous is not null) return new(true, previous.TimelineItemsImported, previous.ArchivedRows, previous.BackupPath);

        var backup = Path.Combine(paths.BackupDirectory, $"legacy-qimiaobot-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{hash[..12]}.db");
        File.Copy(legacyDatabasePath, backup, overwrite: false);
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyDatabasePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await source.OpenAsync(cancellationToken);
        var tables = await ReadTablesAsync(source, cancellationToken);
        if (!tables.Contains("timeline_items")) throw new InvalidDataException("遗留数据库缺少 timeline_items 表。");
        var gameCodes = (await RowsAsync(source, "games", cancellationToken)).ToDictionary(x => Text(x, "id"), x => Text(x, "code"));
        var timelineRows = await RowsAsync(source, "timeline_items", cancellationToken);
        var evidenceRows = await RowsAsync(source, "evidence", cancellationToken);
        await using var tx = await target.Database.BeginTransactionAsync(cancellationToken);
        var run = new LegacyImportRun { SourcePath = Path.GetFullPath(legacyDatabasePath), SourceHash = hash, BackupPath = backup, ImportedAt = DateTimeOffset.UtcNow };
        target.LegacyImportRuns.Add(run);
        var mapping = new Dictionary<string, TimelineItem>();
        foreach (var row in timelineRows)
        {
            var legacyId = Text(row, "id");
            var item = new TimelineItem(gameCodes.GetValueOrDefault(Text(row, "game_id"), "UNKNOWN"), Text(row, "item_type"), Text(row, "title"), Verify(Text(row, "verification_status")),
                NullText(row, "source_time"), NullText(row, "source_timezone"), Date(row, "normalized_time"), Precision(Text(row, "time_precision")), Date(row, "fetched_at") ?? DateTimeOffset.UtcNow, Date(row, "end_at"));
            foreach (var evidence in evidenceRows.Where(x => Text(x, "timeline_item_id") == legacyId))
                item.AddEvidence(new EvidenceRecord(Text(evidence, "source_provider"), Text(evidence, "source_type"), Text(evidence, "source_url"), Text(evidence, "excerpt"), Text(evidence, "parser_version"), Date(evidence, "fetched_at") ?? DateTimeOffset.UtcNow));
            var status = Text(row, "review_status");
            if (status == "CONFIRMED" && item.Evidence.Count > 0) item.Confirm("legacy-import", "保留遗留审核状态", DateTimeOffset.UtcNow);
            else if (status == "RETURNED") item.ReturnToReview("legacy-import", "保留遗留审核状态");
            else if (status == "ARCHIVED") item.Archive("legacy-import", "保留遗留审核状态");
            target.TimelineItems.Add(item); mapping[legacyId] = item;
        }
        await target.SaveChangesAsync(cancellationToken);
        foreach (var row in await RowsAsync(source, "review_actions", cancellationToken)) if (mapping.TryGetValue(Text(row, "timeline_item_id"), out var item))
            target.ReviewActions.Add(new(Guid.NewGuid(), item.Id, Text(row, "action"), Text(row, "actor"), Text(row, "reason"), Date(row, "created_at") ?? DateTimeOffset.UtcNow));
        foreach (var row in await RowsAsync(source, "timeline_item_revisions", cancellationToken)) if (mapping.TryGetValue(Text(row, "timeline_item_id"), out var item))
            target.TimelineItemRevisions.Add(new(Guid.NewGuid(), item.Id, "legacy_snapshot", NullText(row, "old_value") ?? "", NullText(row, "new_value") ?? "", Text(row, "changed_by"), Text(row, "reason"), Date(row, "changed_at") ?? DateTimeOffset.UtcNow));
        var archived = 0;
        foreach (var table in tables.Where(x => x != "sqlite_sequence")) foreach (var row in await RowsAsync(source, table, cancellationToken))
        { target.LegacyArchiveRecords.Add(new LegacyArchiveRecord { ImportRunId = run.Id, TableName = table, RowKey = Text(row, "id", Guid.NewGuid().ToString("N")), PayloadJson = JsonSerializer.Serialize(row) }); archived++; }
        run.TimelineItemsImported = mapping.Count; run.ArchivedRows = archived;
        await target.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(false, mapping.Count, archived, backup);
    }

    private static async Task<HashSet<string>> ReadTablesAsync(SqliteConnection c, CancellationToken ct) { await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'"; await using var r = await cmd.ExecuteReaderAsync(ct); var set = new HashSet<string>(StringComparer.Ordinal); while (await r.ReadAsync(ct)) set.Add(r.GetString(0)); return set; }
    private static async Task<List<Dictionary<string, object?>>> RowsAsync(SqliteConnection c, string table, CancellationToken ct) { var tables = await ReadTablesAsync(c, ct); if (!tables.Contains(table)) return []; await using var cmd = c.CreateCommand(); cmd.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\""; await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<Dictionary<string, object?>>(); while (await r.ReadAsync(ct)) { var row = new Dictionary<string, object?>(); for (var i=0;i<r.FieldCount;i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i); rows.Add(row); } return rows; }
    private static string Text(IReadOnlyDictionary<string, object?> row, string key, string fallback = "") => row.TryGetValue(key, out var value) && value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback : fallback;
    private static string? NullText(IReadOnlyDictionary<string, object?> row, string key) => row.TryGetValue(key, out var value) && value is not null ? Text(row, key) : null;
    private static DateTimeOffset? Date(IReadOnlyDictionary<string, object?> row, string key) => DateTimeOffset.TryParse(NullText(row, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
    private static VerificationStatus Verify(string value) => value switch { "VERIFIED_OFFICIAL" => VerificationStatus.VerifiedOfficial, "VERIFIED_MULTI_SOURCE" => VerificationStatus.VerifiedMultiSource, "CONFLICT" => VerificationStatus.Conflict, _ => VerificationStatus.Unverified };
    private static TimePrecision Precision(string value) => value switch { "EXACT" => TimePrecision.Exact, "RELATIVE" => TimePrecision.Relative, _ => TimePrecision.DateOnly };
    private static async Task<string> HashAsync(string path, CancellationToken ct) { await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); return Convert.ToHexString(await SHA256.HashDataAsync(file, ct)); }
}
