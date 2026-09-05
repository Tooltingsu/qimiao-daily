namespace QimiaoDaily.Data;

public sealed class ReportDraftEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly ReportDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ReportSectionEntity> Sections { get; set; } = [];
}

public sealed class ReportSectionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportDraftId { get; set; }
    public string Key { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Dirty { get; set; }
    public bool ManualOverride { get; set; }
    public bool IsDeleted { get; set; }
}
