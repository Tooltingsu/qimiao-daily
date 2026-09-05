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
                    if (videos.Any(x => x.SourceUrl == url)) continue;
                    videos.Add(new(url, game, c.ItemType, c.Title, url, c.NormalizedTime, "PENDING", now));
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
                repository.Write(artworks, "collected", "artwork.json");
                statuses.Add(new("Pixiv", status, $"Added {added} metadata candidates; no original images downloaded.", now, status != "HEALTHY" && artworks.Count > 0));
            }
            catch (Exception ex) { statuses.Add(new("Pixiv", "FAILED", SafeError(ex), now, artworks.Count > 0)); }
        }
        repository.Write(statuses, "collected", "provider-status.json");
    }

    private static string SafeError(Exception ex) => ex switch
    {
        HttpRequestException http => $"HTTP source failure ({http.StatusCode?.ToString() ?? "network/timeout"})",
        TaskCanceledException => "Source request timed out",
        InvalidDataException => ex.Message,
        _ => ex.GetType().Name
    };
}
