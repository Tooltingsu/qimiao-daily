using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Web;

public sealed record DashboardData(
    DateOnly Date,
    string State,
    string PublishTime,
    int Revision,
    DateTimeOffset? GeneratedAt,
    DateTimeOffset? PublishedAt,
    string Health,
    IReadOnlyDictionary<string, int> ManualCounts,
    IReadOnlyDictionary<string, int> AutomaticCounts,
    int ArtworkPending,
    int ConflictCount,
    string RepositoryUrl,
    IReadOnlyList<ProviderStatusRecord> Providers,
    QqTestDashboardStatus QqTest);

public sealed record ArtworkReviewItem(
    string Platform,
    string ArtworkId,
    string Character,
    string Franchise,
    string Title,
    string Author,
    string SourceUrl,
    string ThumbnailUrl,
    string PreviewUrl,
    string ReviewStatus,
    int? QueueOrder);

public sealed record QqTestDashboardStatus(
    string Environment,
    string Status,
    string? Mode,
    int MessageCount,
    int MediaCount,
    DateTimeOffset? CompletedAt,
    string? Error);

public sealed record WorkspaceData(
    IReadOnlyList<ManualEventRecord> Activities,
    IReadOnlyList<BannerRecord> Banners,
    IReadOnlyList<VersionRecord> Versions,
    IReadOnlyList<EndgameRuleRecord> EndgameRules,
    IReadOnlyList<CalculatedEndgameRecord> CalculatedEndgame,
    IReadOnlyList<BirthdayRecord> Birthdays,
    IReadOnlyList<AnniversaryRecord> Anniversaries,
    IReadOnlyList<ManualCalendarEventRecord> CalendarEvents,
    IReadOnlyList<VideoRecord> Videos,
    IReadOnlyList<BgiCommitRecord> BgiMain,
    IReadOnlyList<BgiCommitRecord> BgiScripts);

public sealed class V4PagesBuilder(V4Repository repository)
{
    public DashboardData Build(DateOnly date)
    {
        var settings = repository.Read<V4Settings>("data", "settings.json");
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.ReadOr<ReportManifest?>(null, "reports", folder, "manifest.json");
        var draft = repository.ReadOr<ReportRevision?>(null, "reports", folder, "draft.json");
        var displayedReport = manifest?.LockedRevision is int lockedRevision
            ? repository.ReadOr<ReportRevision?>(draft, "reports", folder, "revisions", lockedRevision.ToString("000") + ".json")
            : draft;
        var artworks = repository.ReadOr(new List<ArtworkRecord>(), "collected", "artwork.json");
        var providers = repository.ReadOr(new List<ProviderStatusRecord>(), "collected", "provider-status.json");
        var qqTestLog = repository.ReadOr<QqTestPublishLog?>(null, "test-publish-log", folder + ".json");
        var qqTestAttempt = qqTestLog?.Attempts.LastOrDefault();
        var data = new DashboardData(
            date,
            manifest?.State.ToString().ToUpperInvariant() ?? "NOT_GENERATED",
            settings.PublishTime,
            manifest?.LockedRevision ?? manifest?.LatestRevision ?? 0,
            displayedReport?.GeneratedAt,
            manifest?.PublishedAt,
            displayedReport?.Health ?? "UNKNOWN",
            new Dictionary<string, int>
            {
                ["活动"] = repository.Read<List<ManualEventRecord>>("data", "activities.json").Count,
                ["卡池"] = repository.Read<List<BannerRecord>>("data", "banners.json").Count,
                ["版本"] = repository.Read<List<VersionRecord>>("data", "versions.json").Count,
                ["纪念日"] = repository.Read<List<AnniversaryRecord>>("data", "anniversaries.json").Count
                    + repository.Read<List<ManualCalendarEventRecord>>("data", "calendar-events.json").Count(x => x.Enabled)
            },
            new Dictionary<string, int>
            {
                ["周期玩法"] = repository.ReadOr(new List<CalculatedEndgameRecord>(), "generated", "endgame.json").Count,
                ["官方视频"] = repository.ReadOr(new List<VideoRecord>(), "collected", "videos.json").Count,
                ["BGI 本体"] = repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-main.json").Count,
                ["BGI Scripts"] = repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-scripts.json").Count,
                ["美图候选"] = artworks.Count
            },
            artworks.Count(x => x.ReviewStatus.Equals("PENDING", StringComparison.OrdinalIgnoreCase)),
            providers.Count(x => x.Status.Equals("CONFLICT", StringComparison.OrdinalIgnoreCase)),
            settings.RepositoryUrl,
            providers,
            new QqTestDashboardStatus(
                qqTestLog?.Environment ?? "qq-test",
                qqTestAttempt?.Status ?? "NOT_TESTED",
                qqTestAttempt?.Mode,
                qqTestAttempt?.Messages.Count ?? 0,
                qqTestAttempt?.MediaCount ?? 0,
                qqTestAttempt?.CompletedAt,
                qqTestAttempt?.Error));
        repository.Write(data, "web", "data", "dashboard.json");
        repository.WriteText(displayedReport?.Content ?? "今日日报尚未生成。", "web", "data", "report.txt");

        // Pages receives a deliberately public, metadata-only projection for
        // the artwork editor. Collector files and any local cache paths never
        // need to be exposed to the browser.
        var queue = repository.ReadOr(new List<ArtworkQueueEntry>(), "data", "artwork-queue.json");
        var queueOrders = queue.ToDictionary(x => ArtworkKey(x.Platform, x.ArtworkId), x => x.QueueOrder, StringComparer.OrdinalIgnoreCase);
        var previewMap = repository.ReadOr(new Dictionary<string, string>(), "web", "data", "artwork-preview.json");
        var reviewItems = artworks.Select(item => new ArtworkReviewItem(
            item.Platform, item.ArtworkId, item.Character, item.Franchise, item.Title, item.Author,
            PublicHttps(item.SourceUrl), PublicHttps(item.ThumbnailUrl),
            previewMap.TryGetValue(ArtworkKey(item.Platform, item.ArtworkId), out var preview) ? PublicRelativePreview(preview) : string.Empty,
            item.ReviewStatus,
            queueOrders.TryGetValue(ArtworkKey(item.Platform, item.ArtworkId), out var order) ? order : null))
            .OrderBy(x => x.QueueOrder ?? int.MaxValue).ThenBy(x => x.Character, StringComparer.Ordinal)
            .ToList();
        repository.Write(reviewItems, "web", "data", "artwork-review.json");
        repository.Write(queue.OrderBy(x => x.QueueOrder).ToList(), "web", "data", "artwork-queue.json");

        // The Pages workbench mirrors the desktop navigation using the same
        // validated repository data. It is a public read-only projection;
        // edits continue through GitHub or the configured editor service.
        repository.Write(new WorkspaceData(
            repository.ReadOr(new List<ManualEventRecord>(), "data", "activities.json"),
            repository.ReadOr(new List<BannerRecord>(), "data", "banners.json"),
            repository.ReadOr(new List<VersionRecord>(), "data", "versions.json"),
            repository.ReadOr(new List<EndgameRuleRecord>(), "data", "endgame-rules.json"),
            repository.ReadOr(new List<CalculatedEndgameRecord>(), "generated", "endgame.json"),
            repository.ReadOr(new List<BirthdayRecord>(), "data", "birthdays.json"),
            repository.ReadOr(new List<AnniversaryRecord>(), "data", "anniversaries.json"),
            repository.ReadOr(new List<ManualCalendarEventRecord>(), "data", "calendar-events.json"),
            repository.ReadOr(new List<VideoRecord>(), "collected", "videos.json"),
            repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-main.json"),
            repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-scripts.json")),
            "web", "data", "workspace.json");
        return data;
    }
    private static string ArtworkKey(string platform, string artworkId) => platform.Trim() + "\u001f" + artworkId.Trim();
    private static string PublicHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? value : string.Empty;
    private static string PublicRelativePreview(string value) => value.StartsWith("assets/artwork-preview/", StringComparison.Ordinal) ? value : string.Empty;

}
