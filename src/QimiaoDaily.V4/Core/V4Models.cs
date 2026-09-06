using System.Text.Json.Serialization;

namespace QimiaoDaily.V4.Core;

public sealed record ManualEventRecord(string Id, string Game, string Name, DateTimeOffset StartAt, DateTimeOffset EndAt, string Notes, bool Enabled = true);
public sealed record BannerRecord(string Id, string Game, string Name, string Type, DateTimeOffset StartAt, DateTimeOffset EndAt, IReadOnlyList<string> Characters, string Notes, bool Enabled = true);
public sealed record VersionRecord(string Id, string Game, string VersionNumber, string VersionName, DateTimeOffset StartAt, DateTimeOffset EndAt, string Notes, bool Enabled = true);
public sealed record BirthdayRecord(string Id, string Character, string Franchise, int Month, int Day, bool Enabled, string Source, string SourceUrl, string Evidence);
public sealed record AnniversaryRecord(string Id, string Title, DateOnly StartedOn, bool Enabled, string Notes);
public sealed record ManualCalendarEventRecord(
    string Id,
    DateOnly EventDate,
    string Kind,
    string Title,
    string Detail,
    string Source,
    string SourceUrl,
    bool Enabled);

public sealed record EndgameRuleRecord(
    string RuleId,
    string Game,
    string Name,
    string RuleKind,
    DateOnly AnchorDate,
    int IntervalDays,
    string TimePrecision,
    TimeOnly? StartTime,
    bool Enabled = true);

public sealed record EndgameOverrideRecord(
    string RuleId,
    DateOnly ScheduledStart,
    DateOnly? StartsOn,
    TimeOnly? StartTime,
    DateOnly? EndsOn,
    TimeOnly? EndTime,
    bool Suppressed,
    string Notes);

public sealed record ArtworkRecord(
    string Platform,
    string ArtworkId,
    string Character,
    string Franchise,
    string Title,
    string Author,
    string SourceUrl,
    string ThumbnailUrl,
    string ReviewStatus,
    bool SelectedForReport,
    DateTimeOffset PublishedAt,
    DateTimeOffset FetchedAt);

public sealed record VideoRecord(string Id, string Game, string Type, string Title, string SourceUrl, DateTimeOffset? PublishedAt, string ReviewStatus, DateTimeOffset FetchedAt);
public sealed record BgiCommitRecord(string Repository, string Sha, string Subject, string Url, DateTimeOffset? CommittedAt, DateTimeOffset FetchedAt);
public sealed record ProviderStatusRecord(string Provider, string Status, string Message, DateTimeOffset CheckedAt, bool UsedCachedData = false);

public sealed record CalculatedEndgameRecord(
    string RuleId,
    string Game,
    string Name,
    DateOnly ScheduledStart,
    DateOnly StartsOn,
    string TimePrecision,
    TimeOnly? StartTime,
    DateOnly? EndsOn,
    TimeOnly? EndTime,
    string? VersionNumber,
    string Notes);

public sealed record CalendarRecord(DateOnly Date, string Kind, string Title, string Detail);

public sealed class V4Settings
{
    public string TimeZone { get; init; } = "Asia/Shanghai";
    public string PublishTime { get; init; } = "18:30";
    public string DefaultBranch { get; init; } = "main";
    public string RepositoryUrl { get; init; } = "https://github.com/OWNER/qimiao-daily";
    public int ArtworkTargetCount { get; init; } = 30;
    public string[] BgiRepositories { get; init; } = ["babalae/better-genshin-impact", "babalae/bettergi-scripts-list"];
    public string QqChannelId { get; init; } = "BLOCKED_BY_USER";
    public string QqForumId { get; init; } = "BLOCKED_BY_USER";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportState
{
    Draft,
    Validated,
    Ready,
    LockedManual,
    LockedAuto,
    Publishing,
    Published,
    DryRunSucceeded,
    Superseded,
    RepublicationReady,
    Failed
}

public sealed class ReportRevision
{
    public required DateOnly Date { get; init; }
    public required int Revision { get; init; }
    public required ReportState State { get; set; }
    public required string SourceCommit { get; init; }
    public required string ReportHash { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockReason { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public required string Content { get; init; }
    public string Health { get; init; } = "HEALTHY";
    public string ValidationState { get; init; } = "VALID";
    public IReadOnlyList<ArtworkRecord> SelectedArtwork { get; init; } = [];
    public string? PayloadHash { get; init; }
    public IReadOnlyList<ProviderStatusRecord> ProviderStatuses { get; init; } = [];
}

public sealed class ReportManifest
{
    public required DateOnly Date { get; init; }
    public required int LatestRevision { get; set; }
    public int? LockedRevision { get; set; }
    public required ReportState State { get; set; }
    public required string SourceCommit { get; set; }
    public required string ReportHash { get; set; }
    public required DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockReason { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed record PublishAttempt(
    int Revision,
    string ReportHash,
    string SourceCommit,
    string? QqPostId,
    string? QqMessageId,
    DateTimeOffset AttemptedAt,
    DateTimeOffset? PublishedAt,
    string WorkflowRun,
    string Status,
    string? Error,
    bool DryRun,
    string? Reason);

public sealed class PublishLog
{
    public required DateOnly Date { get; init; }
    public List<PublishAttempt> Attempts { get; init; } = [];
}

// qq-test results are intentionally not PublishLog entries. They must never
// cause the production idempotency guard to regard a report as published.
public sealed class QqTestPublishLog
{
    public required DateOnly Date { get; init; }
    public string Environment { get; init; } = "qq-test";
    public List<QqTestPublishAttempt> Attempts { get; init; } = [];
}

public sealed class QqTestPublishAttempt
{
    public string Mode { get; init; } = "auth";
    public string? TargetType { get; init; }
    public string Status { get; init; } = "NOT_TESTED";
    public int? ReportRevision { get; init; }
    public string? ReportHash { get; init; }
    public List<QqTestMessage> Messages { get; init; } = [];
    public int MediaCount { get; init; }
    public string? TestTitlePrefix { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    public DateTimeOffset AttemptedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Error { get; init; }
}

public sealed class QqTestMessage
{
    public int Sequence { get; init; }
    public string Kind { get; init; } = "text";
    public string? MessageId { get; init; }
    public string? PostTaskId { get; init; }
    public string? CreateTime { get; init; }
    public string? Hash { get; init; }
}

public sealed record ValidationIssue(string Level, string File, string Path, string Message);
public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
