using QimiaoDaily.Core;

namespace QimiaoDaily.Data;

public sealed class ArtworkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Platform { get; set; } = string.Empty;
    public string ArtworkId { get; set; } = string.Empty;
    public string NormalizedUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string FranchiseName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string? ThumbnailSha256 { get; set; }
    public string? PerceptualHash { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? SourceMetadata { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public bool SelectedForReport { get; set; }
}

public sealed class SeenArtworkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Platform { get; set; } = string.Empty;
    public string ArtworkId { get; set; } = string.Empty;
    public string NormalizedUrl { get; set; } = string.Empty;
    public string? ContentSha256 { get; set; }
    public string? PerceptualHash { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
}

public sealed class ArtworkReviewActionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArtworkId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ArtworkRevisionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArtworkId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ArtworkDailyRunEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "Pixiv";
    public int TargetCount { get; set; } = 30;
    public int FetchedCount { get; set; }
    public int NewCandidateCount { get; set; }
    public string Status { get; set; } = "NOT_RUN";
    public string? FailureReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}
