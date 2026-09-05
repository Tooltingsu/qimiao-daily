namespace QimiaoDaily.Core;

public enum ReviewStatus { Pending, Confirmed, Returned, Archived }
public enum VerificationStatus { VerifiedOfficial, VerifiedMultiSource, Unverified, Conflict }
public enum TimePrecision { Exact, DateOnly, Relative }
public enum TimelineChangeKind { None, New, TimeChanged, ContentChanged, SourceChanged, Conflict }

public sealed class TimelineItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string GameCode { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public ReviewStatus ReviewStatus { get; private set; } = ReviewStatus.Pending;
    public VerificationStatus VerificationStatus { get; private set; } = VerificationStatus.Unverified;
    public string? SourceTime { get; private set; }
    public string? SourceTimezone { get; private set; }
    public DateTimeOffset? NormalizedTime { get; private set; }
    public DateTimeOffset? EndAt { get; private set; }
    public TimePrecision TimePrecision { get; private set; } = TimePrecision.DateOnly;
    public TimePrecision StartTimePrecision { get; private set; } = TimePrecision.DateOnly;
    public TimePrecision EndTimePrecision { get; private set; } = TimePrecision.DateOnly;
    public string? StartTimeSource { get; private set; }
    public string? EndTimeSource { get; private set; }
    public string? StartExpression { get; private set; }
    public string? EndExpression { get; private set; }
    public string? StartTimeEvidenceKey { get; private set; }
    public string? EndTimeEvidenceKey { get; private set; }
    public string? GachaPoolKind { get; private set; }
    public string? GachaPoolPhase { get; private set; }
    public string? GachaGroupKey { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public string CanonicalIdentity { get; private set; } = string.Empty;
    public TimelineChangeKind ChangeKind { get; private set; } = TimelineChangeKind.New;
    public DataOrigin DataOrigin { get; private set; } = DataOrigin.AutoCollected;
    public bool UserConfirmed { get; private set; }
    public List<EvidenceRecord> Evidence { get; } = [];

    private TimelineItem() { }

    public TimelineItem(string gameCode, string itemType, string title, VerificationStatus verificationStatus, string? sourceTime, string? sourceTimezone, DateTimeOffset? normalizedTime, TimePrecision timePrecision, DateTimeOffset fetchedAt, DateTimeOffset? endAt = null,
        TimePrecision? startTimePrecision = null, TimePrecision? endTimePrecision = null, string? startTimeSource = null, string? endTimeSource = null,
        string? startExpression = null, string? endExpression = null, string? startTimeEvidenceKey = null, string? endTimeEvidenceKey = null,
        string? gachaPoolKind = null, string? gachaPoolPhase = null, string? gachaGroupKey = null)
    {
        if (string.IsNullOrWhiteSpace(gameCode) || string.IsNullOrWhiteSpace(itemType) || string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Game, type and title are required.");
        GameCode = gameCode.Trim(); ItemType = itemType.Trim(); Title = title.Trim(); VerificationStatus = verificationStatus; SourceTime = sourceTime; SourceTimezone = sourceTimezone; NormalizedTime = normalizedTime; EndAt = endAt; TimePrecision = timePrecision; StartTimePrecision = startTimePrecision ?? timePrecision; EndTimePrecision = endTimePrecision ?? (endAt is null ? TimePrecision.Relative : timePrecision); StartTimeSource = startTimeSource; EndTimeSource = endTimeSource; StartExpression = startExpression; EndExpression = endExpression; StartTimeEvidenceKey = startTimeEvidenceKey; EndTimeEvidenceKey = endTimeEvidenceKey; FetchedAt = fetchedAt;
        if (string.Equals(ItemType, "GACHA", StringComparison.OrdinalIgnoreCase)) { GachaPoolKind = gachaPoolKind?.Trim(); GachaPoolPhase = gachaPoolPhase?.Trim(); GachaGroupKey = gachaGroupKey?.Trim(); }
    }

    public void AddEvidence(EvidenceRecord evidence) => Evidence.Add(evidence ?? throw new ArgumentNullException(nameof(evidence)));

    public void SetCanonicalIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Canonical identity is required.");
        CanonicalIdentity = identity.Trim();
    }

    public void SetChangeKind(TimelineChangeKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Timeline change kind is invalid.");
        ChangeKind = kind;
    }

    /// <summary>Records the source classification assigned by a controlled migration or a formal workflow.</summary>
    public void SetDataProvenance(DataOrigin dataOrigin, bool userConfirmed)
    {
        if (!Enum.IsDefined(dataOrigin)) throw new ArgumentException("Data origin is invalid.", nameof(dataOrigin));
        DataOrigin = dataOrigin;
        UserConfirmed = userConfirmed;
    }

    public IReadOnlyList<TimelineFieldChange> Edit(string itemType, string title, VerificationStatus verificationStatus,
        string? sourceTime, string? sourceTimezone, DateTimeOffset? normalizedTime, TimePrecision timePrecision,
        DateTimeOffset fetchedAt, DateTimeOffset? endAt, TimePrecision? startTimePrecision = null, TimePrecision? endTimePrecision = null,
        string? startTimeSource = null, string? endTimeSource = null, string? startExpression = null, string? endExpression = null,
        string? startTimeEvidenceKey = null, string? endTimeEvidenceKey = null,
        string? gachaPoolKind = null, string? gachaPoolPhase = null, string? gachaGroupKey = null)
    {
        if (string.IsNullOrWhiteSpace(itemType) || string.IsNullOrWhiteSpace(title)) throw new ArgumentException("类别和标题不能为空。");
        if (!Enum.IsDefined(verificationStatus) || !Enum.IsDefined(timePrecision)) throw new ArgumentException("验证状态或时间精度无效。");
        if (normalizedTime is not null && endAt is not null && endAt < normalizedTime) throw new ArgumentException("结束时间不能早于开始时间。");
        var changes = new List<TimelineFieldChange>();
        Change(changes, "ItemType", ItemType, itemType.Trim(), value => ItemType = value);
        Change(changes, "Title", Title, title.Trim(), value => Title = value);
        Change(changes, "VerificationStatus", VerificationStatus.ToString(), verificationStatus.ToString(), value => VerificationStatus = Enum.Parse<VerificationStatus>(value));
        Change(changes, "SourceTime", SourceTime ?? string.Empty, sourceTime?.Trim() ?? string.Empty, value => SourceTime = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "SourceTimezone", SourceTimezone ?? string.Empty, sourceTimezone?.Trim() ?? string.Empty, value => SourceTimezone = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "NormalizedTime", NormalizedTime?.ToString("O") ?? string.Empty, normalizedTime?.ToString("O") ?? string.Empty, value => NormalizedTime = string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value));
        Change(changes, "TimePrecision", TimePrecision.ToString(), timePrecision.ToString(), value => TimePrecision = Enum.Parse<TimePrecision>(value));
        Change(changes, "StartTimePrecision", StartTimePrecision.ToString(), (startTimePrecision ?? timePrecision).ToString(), value => StartTimePrecision = Enum.Parse<TimePrecision>(value));
        Change(changes, "EndTimePrecision", EndTimePrecision.ToString(), (endTimePrecision ?? (endAt is null ? TimePrecision.Relative : timePrecision)).ToString(), value => EndTimePrecision = Enum.Parse<TimePrecision>(value));
        Change(changes, "StartTimeSource", StartTimeSource ?? string.Empty, startTimeSource ?? string.Empty, value => StartTimeSource = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "EndTimeSource", EndTimeSource ?? string.Empty, endTimeSource ?? string.Empty, value => EndTimeSource = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "StartExpression", StartExpression ?? string.Empty, startExpression ?? string.Empty, value => StartExpression = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "EndExpression", EndExpression ?? string.Empty, endExpression ?? string.Empty, value => EndExpression = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "StartTimeEvidenceKey", StartTimeEvidenceKey ?? string.Empty, startTimeEvidenceKey ?? string.Empty, value => StartTimeEvidenceKey = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "EndTimeEvidenceKey", EndTimeEvidenceKey ?? string.Empty, endTimeEvidenceKey ?? string.Empty, value => EndTimeEvidenceKey = string.IsNullOrEmpty(value) ? null : value);
        var effectiveKind = string.Equals(itemType, "GACHA", StringComparison.OrdinalIgnoreCase) ? gachaPoolKind?.Trim() : null;
        var effectivePhase = string.Equals(itemType, "GACHA", StringComparison.OrdinalIgnoreCase) ? gachaPoolPhase?.Trim() : null;
        var effectiveGroup = string.Equals(itemType, "GACHA", StringComparison.OrdinalIgnoreCase) ? gachaGroupKey?.Trim() : null;
        Change(changes, "GachaPoolKind", GachaPoolKind ?? string.Empty, effectiveKind ?? string.Empty, value => GachaPoolKind = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "GachaPoolPhase", GachaPoolPhase ?? string.Empty, effectivePhase ?? string.Empty, value => GachaPoolPhase = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "GachaGroupKey", GachaGroupKey ?? string.Empty, effectiveGroup ?? string.Empty, value => GachaGroupKey = string.IsNullOrEmpty(value) ? null : value);
        Change(changes, "EndAt", EndAt?.ToString("O") ?? string.Empty, endAt?.ToString("O") ?? string.Empty, value => EndAt = string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value));
        Change(changes, "FetchedAt", FetchedAt.ToString("O"), fetchedAt.ToString("O"), value => FetchedAt = DateTimeOffset.Parse(value));
        if (ReviewStatus != ReviewStatus.Pending)
        {
            changes.Add(new TimelineFieldChange("ReviewStatus", ReviewStatus.ToString(), ReviewStatus.Pending.ToString()));
            ReviewStatus = ReviewStatus.Pending;
        }
        return changes;
    }

    private static void Change(List<TimelineFieldChange> changes, string field, string oldValue, string newValue, Action<string> apply)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;
        changes.Add(new TimelineFieldChange(field, oldValue, newValue));
        apply(newValue);
    }

    public void Confirm(string actor, string reason, DateTimeOffset at)
    {
        if (ReviewStatus == ReviewStatus.Archived) throw new InvalidOperationException("Archived content cannot be confirmed.");
        if (Evidence.Count == 0) throw new InvalidOperationException("A candidate without Evidence cannot be confirmed.");
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Confirmation requires actor and reason.");
        ReviewStatus = ReviewStatus.Confirmed;
        foreach (var evidence in Evidence) evidence.MarkVerified(VerificationStatus);
    }

    public void ReturnToReview(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Returning requires actor and reason.");
        ReviewStatus = ReviewStatus.Pending;
    }

    public void RestoreFromArchive(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Restoring requires actor and reason.");
        if (ReviewStatus != ReviewStatus.Archived) throw new InvalidOperationException("Only archived content can be restored.");
        ReviewStatus = ReviewStatus.Pending;
    }

    public void Archive(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Archiving requires actor and reason.");
        ReviewStatus = ReviewStatus.Archived;
    }
}

public sealed class EvidenceRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TimelineItemId { get; private set; }
    public string SourceProvider { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string? PageTitle { get; private set; }
    public string SourceText { get; private set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? OriginalTimezone { get; private set; }
    public DateTimeOffset? NormalizedTime { get; private set; }
    public string ParserVersion { get; private set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; private set; } = VerificationStatus.Unverified;
    public DateTimeOffset FetchedAt { get; private set; }

    private EvidenceRecord() { }

    public EvidenceRecord(string sourceProvider, string sourceType, string sourceUrl, string sourceText, string parserVersion, DateTimeOffset fetchedAt, string? pageTitle = null, DateTimeOffset? publishedAt = null, string? originalTimezone = null, DateTimeOffset? normalizedTime = null, VerificationStatus verificationStatus = VerificationStatus.Unverified)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider) || string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(sourceText)) throw new ArgumentException("Evidence requires provider, URL and source text.");
        SourceProvider = sourceProvider.Trim(); SourceType = sourceType.Trim(); SourceUrl = sourceUrl.Trim(); PageTitle = pageTitle?.Trim(); SourceText = sourceText.Trim(); PublishedAt = publishedAt; OriginalTimezone = originalTimezone?.Trim(); NormalizedTime = normalizedTime; ParserVersion = parserVersion.Trim(); VerificationStatus = verificationStatus; FetchedAt = fetchedAt;
    }

    public void MarkVerified(VerificationStatus status) => VerificationStatus = status;
}

public sealed record ReviewAction(Guid Id, Guid TimelineItemId, string Action, string Actor, string Reason, DateTimeOffset CreatedAt);
public sealed record TimelineItemRevision(Guid Id, Guid TimelineItemId, string FieldName, string OldValue, string NewValue, string Actor, string Reason, DateTimeOffset CreatedAt);
public sealed record TimelineFieldChange(string FieldName, string OldValue, string NewValue);

public static class ReportEligibility
{
    public static bool CanInclude(TimelineItem item) => item.ReviewStatus == ReviewStatus.Confirmed && item.VerificationStatus is VerificationStatus.VerifiedOfficial or VerificationStatus.VerifiedMultiSource;
}
