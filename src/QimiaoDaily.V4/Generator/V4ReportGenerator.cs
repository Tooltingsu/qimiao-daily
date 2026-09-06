using System.Security.Cryptography;
using System.Text;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Generator;

public sealed class V4ReportGenerator(V4Repository repository)
{
    public ReportRevision Generate(DateOnly date, string sourceCommit, DateTimeOffset now)
    {
        var reportDirectory = repository.PathFor("reports", date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(Path.Combine(reportDirectory, "revisions"));
        var manifest = repository.ReadOr<ReportManifest?>(null, "reports", date.ToString("yyyy-MM-dd"), "manifest.json");
        var revision = (manifest?.LatestRevision ?? 0) + 1;
        var providerStatuses = repository.ReadOr(new List<ProviderStatusRecord>(), "collected", "provider-status.json");
        var content = Compose(date);
        var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        var health = providerStatuses.Any(x => !x.Status.Equals("HEALTHY", StringComparison.OrdinalIgnoreCase)) ? "DEGRADED" : "HEALTHY";
        // The confirmed area is a FIFO queue, not a collection of images to
        // send all at once.  Snapshot only its head into an immutable daily
        // revision.  The entry is consumed only after a real production
        // publication succeeds; generating or locking must never advance it.
        var artwork = ConfirmedArtworkQueue()
            .Take(1)
            .Select(PublicArtworkMetadata)
            .ToList();
        var report = new ReportRevision
        {
            Date = date,
            Revision = revision,
            State = ReportState.Ready,
            SourceCommit = sourceCommit,
            ReportHash = hash,
            GeneratedAt = now,
            Content = content,
            Health = health,
            SelectedArtwork = artwork,
            PayloadHash = PayloadHash(content, artwork),
            ProviderStatuses = providerStatuses
        };
        repository.Write(report, "reports", date.ToString("yyyy-MM-dd"), "revisions", revision.ToString("000") + ".json");
        repository.Write(report, "reports", date.ToString("yyyy-MM-dd"), "draft.json");
        repository.WriteText(content + Environment.NewLine, "reports", date.ToString("yyyy-MM-dd"), "preview.txt");
        var hasLockedRevision = manifest?.LockedRevision is not null;
        repository.Write(new ReportManifest
        {
            Date = date,
            LatestRevision = revision,
            LockedRevision = manifest?.LockedRevision,
            State = hasLockedRevision ? manifest!.State : ReportState.Ready,
            SourceCommit = hasLockedRevision ? manifest!.SourceCommit : sourceCommit,
            ReportHash = hasLockedRevision ? manifest!.ReportHash : hash,
            GeneratedAt = hasLockedRevision ? manifest!.GeneratedAt : now,
            LockedAt = manifest?.LockedAt,
            LockReason = manifest?.LockReason,
            PublishedAt = manifest?.PublishedAt
        }, "reports", date.ToString("yyyy-MM-dd"), "manifest.json");
        return report;
    }

    private string Compose(DateOnly date)
    {
        var lines = new List<string>
        {
            $"绮喵日报 {date:yyMMdd}",
            string.Empty,
            $"今天是{date.Year}年{date.Month}月{date.Day}日，星期{Weekday(date.DayOfWeek)}"
        };

        var calendar = repository.ReadOr(new List<CalendarRecord>(), "generated", "calendar.json").Where(x => x.Date == date).ToList();
        foreach (var item in calendar.Where(x => x.Kind == "SOLAR_TERM")) lines.Add($"今天是二十四节气 {item.Title}");
        foreach (var item in calendar.Where(x => x.Kind == "FESTIVAL")) lines.Add($"今天是节日 {item.Title}");
        foreach (var item in calendar.Where(x => x.Kind == "BIRTHDAY"))
        {
            var franchise = DisplayGame(item.Detail);
            lines.Add(string.IsNullOrWhiteSpace(franchise)
                ? $"今天是【{item.Title}】的生日"
                : $"今天是【{franchise} {item.Title}】的生日");
        }
        foreach (var item in calendar.Where(x => x.Kind == "ANNIVERSARY")) lines.Add($"今天是【{item.Title}】{item.Detail}纪念日");
        foreach (var item in calendar.Where(x => x.Kind == "MEMORIAL")) lines.Add($"今天是纪念日 {item.Title}");
        foreach (var item in calendar.Where(x => x.Kind is not ("SOLAR_TERM" or "FESTIVAL" or "BIRTHDAY" or "ANNIVERSARY" or "MEMORIAL")))
            lines.Add("今天是 " + item.Title + (string.IsNullOrWhiteSpace(item.Detail) ? string.Empty : " " + item.Detail));

        lines.AddRange([string.Empty, "游戏活动预览"]);
        var events = repository.Read<List<ManualEventRecord>>("data", "activities.json")
            .Where(x => x.Enabled && DateOnly.FromDateTime(x.StartAt.DateTime) <= date && DateOnly.FromDateTime(x.EndAt.DateTime) >= date);
        foreach (var item in events) lines.Add($"-{DisplayGame(item.Game)} {item.Name}（{Local(item.StartAt):MM-dd HH:mm}～{Local(item.EndAt):MM-dd HH:mm}）");
        var banners = repository.Read<List<BannerRecord>>("data", "banners.json")
            .Where(x => x.Enabled && DateOnly.FromDateTime(x.StartAt.DateTime) <= date && DateOnly.FromDateTime(x.EndAt.DateTime) >= date);
        foreach (var item in banners) lines.Add($"-{DisplayGame(item.Game)} {item.Name}【{string.Join("、", item.Characters)}】（{Local(item.StartAt):MM-dd HH:mm}～{Local(item.EndAt):MM-dd HH:mm}）");
        foreach (var item in repository.ReadOr(new List<CalculatedEndgameRecord>(), "generated", "endgame.json").Where(x => x.StartsOn == date))
            lines.Add($"-{DisplayGame(item.Game)} {item.Name} 今日刷新");

        var videos = repository.ReadOr(new List<VideoRecord>(), "collected", "videos.json")
            .Where(x => x.ReviewStatus.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase) && x.PublishedAt is { } published && DateOnly.FromDateTime(Local(published).DateTime) == date);
        foreach (var item in videos) lines.Add($"-{DisplayGame(item.Game)} {item.Title} {item.SourceUrl}");

        lines.AddRange([string.Empty, "BGI 更新"]);
        foreach (var item in repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-main.json")) lines.Add($"-{item.Subject} ({Short(item.Sha)})");
        foreach (var item in repository.ReadOr(new List<BgiCommitRecord>(), "collected", "bgi-scripts.json")) lines.Add($"-{item.Subject} ({Short(item.Sha)})");

        var artworks = ConfirmedArtworkQueue().Take(1).ToList();
        if (artworks.Count > 0)
        {
            lines.AddRange([string.Empty, "美图分享"]);
            lines.AddRange(artworks.Select(x => $"{x.Character} {DisplayGame(x.Franchise)} 来源：{x.Platform.ToLowerInvariant()}"));
        }
        return string.Join("\n", lines).TrimEnd();
    }

    private static DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
    public static string PayloadHash(string content, IReadOnlyList<ArtworkRecord> artwork)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { content, artwork }, V4Repository.JsonOptions)))).ToLowerInvariant();
    private static string Short(string value) => value[..Math.Min(7, value.Length)];
    private IEnumerable<ArtworkRecord> ConfirmedArtworkQueue()
    {
        var entries = repository.Read<List<ArtworkQueueEntry>>("data", "artwork-queue.json");
        var duplicateOrder = entries.Where(x => x.QueueOrder > 0)
            .GroupBy(x => x.QueueOrder)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateOrder is not null)
            throw new InvalidDataException($"Confirmed artwork queueOrder {duplicateOrder.Key} is duplicated.");
        var candidates = repository.ReadOr(new List<ArtworkRecord>(), "collected", "artwork.json")
            .ToDictionary(x => ArtworkKey(x.Platform, x.ArtworkId), StringComparer.OrdinalIgnoreCase);
        return entries.OrderBy(x => x.QueueOrder)
            .Select(entry => candidates.TryGetValue(ArtworkKey(entry.Platform, entry.ArtworkId), out var candidate)
                ? candidate with { ReviewStatus = "CONFIRMED", SelectedForReport = true }
                : throw new InvalidDataException($"Confirmed artwork {entry.Platform}/{entry.ArtworkId} is missing from collected metadata."));
    }
    private static string ArtworkKey(string platform, string artworkId) => platform.Trim() + "\u001f" + artworkId.Trim();
    private static ArtworkRecord PublicArtworkMetadata(ArtworkRecord artwork)
    {
        // Reports are public Git artifacts.  A legacy desktop database can
        // contain a local cache filename, so only an https thumbnail is safe
        // to snapshot into a report revision.
        var thumbnail = Uri.TryCreate(artwork.ThumbnailUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? artwork.ThumbnailUrl : string.Empty;
        return artwork with { ThumbnailUrl = thumbnail };
    }
    private static string DisplayGame(string value) => value.Trim().ToUpperInvariant() switch
    {
        "GENSHIN" => "原神", "STARRAIL" => "崩坏：星穹铁道", "NTE" => "异环", "HI3" => "崩坏3", "ZZZ" => "绝区零", "WUWA" => "鸣潮", _ => value
    };
    private static string Weekday(DayOfWeek day) => "日一二三四五六"[(int)day].ToString();
}
