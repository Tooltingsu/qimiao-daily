namespace QimiaoDaily.Data;

public sealed class LegacyImportRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourcePath { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
    public int TimelineItemsImported { get; set; }
    public int ArchivedRows { get; set; }
}

public sealed class LegacyArchiveRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportRunId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public sealed record LegacyImportResult(bool AlreadyImported, int TimelineItemsImported, int ArchivedRows, string BackupPath);
