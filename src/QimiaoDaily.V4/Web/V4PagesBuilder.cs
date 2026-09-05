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
    IReadOnlyList<ProviderStatusRecord> Providers);

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
            providers);
        repository.Write(data, "web", "data", "dashboard.json");
        repository.WriteText(displayedReport?.Content ?? "今日日报尚未生成。", "web", "data", "report.txt");
        return data;
    }
}
