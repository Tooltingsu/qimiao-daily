using QimiaoDaily.Collectors;
using QimiaoDaily.Services;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Collectors;

public sealed class V4MediaCollector(V4Repository repository, HttpClient client)
{
    public async Task CollectAsync(DateOnly date, DateTimeOffset now)
    {
        var statuses = repository.ReadOr(new List<ProviderStatusRecord>(), "collected", "provider-status.json")
            .Where(x => !x.Provider.StartsWith("Video:") && x.Provider != "Pixiv").ToList();
        var videos = repository.ReadOr(new List<VideoRecord>(), "collected", "videos.json");
        foreach (var game in new[] { "GENSHIN", "STARRAIL", "NTE" })
        {
            try
            {
                IReadOnlyList<GameCandidate> candidates;
                if (game == "NTE")
                {
                    var result = await new NteBilibiliOfficialProvider(client).CollectAsync();
                    if (result.Status != SourceFetchStatus.Healthy) throw new InvalidDataException(result.Message);
                    candidates = result.Candidates;
                }
                else
                    candidates = await new OfficialYoutubeRssProvider(client).CollectAsync(game,
                        game == "GENSHIN" ? OfficialYoutubeRssProvider.GenshinChannelId : OfficialYoutubeRssProvider.StarRailChannelId,
                        game == "GENSHIN" ? "Genshin Impact" : "Honkai: Star Rail");
                foreach (var c in candidates)
                {
                    var url = c.Evidence.FirstOrDefault()?.SourceUrl ?? "";
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    var index = videos.FindIndex(x => x.SourceUrl == url);
                    var officialVideo = new VideoRecord(url, game, c.ItemType, c.Title, url, c.NormalizedTime, "CONFIRMED", now);
                    if (index < 0)
                        videos.Add(officialVideo);
                    else
                        // Official RSS/Bilibili candidates are the automatic
                        // source of truth for videos.  A prior collection used
                        // PENDING here, which accidentally hid every video
                        // from the daily report and required a review UI that
                        // V4 intentionally does not have.
                        videos[index] = officialVideo;
                }
                statuses.Add(new("Video:" + game, "HEALTHY", $"Fetched {candidates.Count} official candidates.", now));
            }
            catch (Exception ex)
            {
                var cached = videos.Any(x => x.Game == game);
                statuses.Add(new("Video:" + game, cached ? "DEGRADED" : "FAILED", SafeError(ex), now, cached));
            }
        }
        repository.Write(videos.OrderBy(x => x.Game).ThenBy(x => x.SourceUrl).ToList(), "collected", "videos.json");
        var artworks = repository.ReadOr(new List<ArtworkRecord>(), "collected", "artwork.json");
        var session = Environment.GetEnvironmentVariable("PIXIV_SESSION");
        if (string.IsNullOrWhiteSpace(session))
            statuses.Add(new("Pixiv", "LOGIN_REQUIRED", "PIXIV_SESSION is not configured; retained metadata cache.", now, artworks.Count > 0));
        else
        {
            try
            {
                var provider = new PixivArtworkProvider(client, session);
                var settings = repository.Read<V4Settings>("data", "settings.json");
                var replaceCandidates = string.Equals(Environment.GetEnvironmentVariable("INPUT_REPLACE_ARTWORK_CANDIDATES"), "true", StringComparison.OrdinalIgnoreCase);
                // A manual refresh replaces the review inbox. Never discard queue entries:
                // they are user-approved FIFO choices and must remain publishable.
                var queueKeys = repository.ReadOr(new List<ArtworkQueueEntry>(), "data", "artwork-queue.json")
                    .OrderBy(x => x.QueueOrder)
                    .Select(x => ArtworkKey(x.Platform, x.ArtworkId))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (replaceCandidates)
                    artworks = artworks.Where(x => queueKeys.Contains(ArtworkKey(x.Platform, x.ArtworkId))).ToList();
                var status = "HEALTHY";
                var added = 0;
                foreach (var search in ArtworkCharacterCatalog.GetDailySelection(settings.ArtworkTargetCount, date))
                {
                    var result = await provider.SearchAsync(search);
                    if (result.Status != ArtworkFetchStatus.Healthy)
                    {
                        status = result.Status switch { ArtworkFetchStatus.LoginRequired => "LOGIN_REQUIRED", ArtworkFetchStatus.Blocked => "BLOCKED", _ => "FAILED" };
                        if (result.Message.Contains("rate-limited")) status = "RATE_LIMITED";
                        break;
                    }
                    foreach (var c in result.Candidates.Where(c => artworks.All(x => x.ArtworkId != c.ArtworkId)))
                    {
                        artworks.Add(new(c.Platform, c.ArtworkId, c.CharacterName ?? "", c.FranchiseName ?? "", c.Title, c.Author,
                            c.SourceUrl, c.ThumbnailUrl, "PENDING", false, c.PublishedAt, c.FetchedAt));
                        added++;
                    }
                }
                // Older metadata-only candidates predate the web review UI and
                // have no thumbnail. Once an authorized session is available,
                // backfill the current queue first, then a bounded number of
                // visible candidates. This requests only Pixiv metadata, never
                // original artwork files.
                var backfill = artworks.Where(x => string.IsNullOrWhiteSpace(x.ThumbnailUrl))
                    .OrderByDescending(x => queueKeys.Contains(ArtworkKey(x.Platform, x.ArtworkId)))
                    .ThenByDescending(x => x.FetchedAt)
                    .Take(Math.Max(1, settings.ArtworkTargetCount))
                    .ToList();
                var refreshed = 0;
                foreach (var prior in backfill)
                {
                    var metadata = await provider.FetchAsync(prior.ArtworkId);
                    if (metadata.Status != ArtworkFetchStatus.Healthy || metadata.Candidate is null)
                    {
                        if (metadata.Status is ArtworkFetchStatus.Blocked or ArtworkFetchStatus.LoginRequired) status = metadata.Status == ArtworkFetchStatus.Blocked ? "BLOCKED" : "LOGIN_REQUIRED";
                        break;
                    }
                    var index = artworks.FindIndex(x => ArtworkKey(x.Platform, x.ArtworkId) == ArtworkKey(prior.Platform, prior.ArtworkId));
                    if (index < 0) continue;
                    var fresh = metadata.Candidate;
                    artworks[index] = prior with
                    {
                        Title = fresh.Title,
                        Author = fresh.Author,
                        SourceUrl = fresh.SourceUrl,
                        ThumbnailUrl = fresh.ThumbnailUrl,
                        PublishedAt = fresh.PublishedAt,
                        FetchedAt = fresh.FetchedAt
                    };
                    refreshed++;
                }
                repository.Write(artworks, "collected", "artwork.json");
                statuses.Add(new("Pixiv", status, $"{(replaceCandidates ? "Replaced unqueued candidates; " : "Added ")}{added} candidates and refreshed {refreshed} thumbnail metadata; no original images downloaded.", now, status != "HEALTHY" && artworks.Count > 0));
            }
            catch (Exception ex) { statuses.Add(new("Pixiv", "FAILED", SafeError(ex), now, artworks.Count > 0)); }
        }
        repository.Write(statuses, "collected", "provider-status.json");
    }

    private static string ArtworkKey(string platform, string artworkId) => platform.Trim() + "\u001f" + artworkId.Trim();

    private static string SafeError(Exception ex) => ex switch
    {
        HttpRequestException http => $"HTTP source failure ({http.StatusCode?.ToString() ?? "network/timeout"})",
        TaskCanceledException => "Source request timed out",
        InvalidDataException => ex.Message,
        _ => ex.GetType().Name
    };
}
