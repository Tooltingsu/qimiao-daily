using QimiaoDaily.Core;

namespace QimiaoDaily.Data;

public sealed class ManualEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Game { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DataOrigin Origin { get; set; } = DataOrigin.Manual;
    public bool UserConfirmed { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BannerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Game { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? CustomType { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DataOrigin Origin { get; set; } = DataOrigin.Manual;
    public bool UserConfirmed { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<BannerCharacterEntity> Characters { get; set; } = [];
}

public sealed class BannerCharacterEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BannerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class GameVersionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Game { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DataOrigin Origin { get; set; } = DataOrigin.Manual;
    public bool UserConfirmed { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EndgameRuleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Stable local schedule key, independent of the database Guid.</summary>
    public string RuleKey { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleKind { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = "{}";
    /// <summary>EXACT or DATE_ONLY. DATE_ONLY must never infer a clock value.</summary>
    public string TimePrecision { get; set; } = "EXACT";
    public TimeOnly? StartTime { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class EndgameAnchorEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    /// <summary>Authoritative date for DATE_ONLY rules.</summary>
    public DateOnly? AnchorDate { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string? VersionNumber { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class EndgameOccurrenceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public Guid? VersionId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    /// <summary>Authoritative dates; StartAt is retained only for legacy readers.</summary>
    public DateOnly? ScheduledDate { get; set; }
    public DateOnly? OccurrenceDate { get; set; }
    public string TimePrecision { get; set; } = "EXACT";
    public TimeOnly? StartTime { get; set; }
    public int Sequence { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Notes { get; set; } = string.Empty;
    public bool IsOverride { get; set; }
}

/// <summary>Import provenance used only to match previewed JSON entries to formal V3 records.</summary>
public sealed class QimiaoImportRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RecordType { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string NaturalKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid FormalEntityId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class ManualDataAuditEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
