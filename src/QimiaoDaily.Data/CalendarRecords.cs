using QimiaoDaily.Core;
namespace QimiaoDaily.Data;
public sealed class BirthdayEntity{public Guid Id{get;set;}=Guid.NewGuid();public string Character{get;set;}=string.Empty;public string CanonicalCharacterNameZhCn{get;set;}=string.Empty;public string Aliases{get;set;}=string.Empty;public string Franchise{get;set;}=string.Empty;public int Month{get;set;}public int Day{get;set;}public string Source{get;set;}=string.Empty;public string SourceTier{get;set;}="official";public string SourceUrl{get;set;}=string.Empty;public string Evidence{get;set;}=string.Empty;public VerificationStatus VerificationStatus{get;set;}public DateTimeOffset VerifiedAt{get;set;}public bool Enabled{get;set;}public DataOrigin DataOrigin{get;set;}=DataOrigin.AutoCollected;public bool UserConfirmed{get;set;}public string OriginTrace{get;set;}=string.Empty;}
public sealed class AnniversaryEntity{public Guid Id{get;set;}=Guid.NewGuid();public string Title{get;set;}=string.Empty;public DateOnly StartedOn{get;set;}public bool Enabled{get;set;}public DataOrigin DataOrigin{get;set;}=DataOrigin.AutoCollected;public bool UserConfirmed{get;set;}public string Notes{get;set;}=string.Empty;}
public sealed class CalendarEventEntity{public Guid Id{get;set;}=Guid.NewGuid();public DateOnly EventDate{get;set;}public string Kind{get;set;}="MEMORIAL";public string Title{get;set;}=string.Empty;public string? Detail{get;set;}public string Source{get;set;}="MANUAL";public string? SourceUrl{get;set;}public bool Enabled{get;set;}=true;}

/// <summary>Versioned, evidence-backed rule metadata for an endgame mode.</summary>
public sealed class EndgameCycleRuleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GameCode { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RecurrenceKind { get; set; } = "ANNOUNCEMENT_BACKED";
    public int? IntervalDays { get; set; }
    public DateTimeOffset? AnchorStart { get; set; }
    public string RuleVersion { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>Concrete cycle window linked to the announcement that established it.</summary>
public sealed class EndgameCycleInstanceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string RuleVersion { get; set; } = string.Empty;
    public Guid? TimelineItemId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}
