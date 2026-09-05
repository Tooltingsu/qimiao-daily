using System.Text.Json;

namespace QimiaoDaily.Collectors;

public enum SourceFetchStatus { Healthy, Blocked, Failed }
public sealed record SourceFetchResult(
    SourceFetchStatus Status,
    string Message,
    IReadOnlyList<GameCandidate> Candidates,
    int SourceCandidateCount = 0,
    int SourceRejectedCount = 0,
    IReadOnlyDictionary<string, int>? SourceRejectionReasons = null);

public sealed class NteBilibiliOfficialProvider(HttpClient client)
{
    public const string OfficialMid = "3546636978489848";
    public const string FeedUrl = "https://api.bilibili.com/x/space/arc/search?mid=3546636978489848&pn=1&ps=30&order=pubdate";

    public async Task<GameCandidate> VerifyOfficialVideoAsync(string bvid, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.bilibili.com/x/web-interface/view?bvid={Uri.EscapeDataString(bvid)}";
        using var response = await client.GetAsync(url, cancellationToken); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (json.RootElement.GetProperty("code").GetInt32() != 0) throw new InvalidDataException("Bilibili video endpoint returned a non-zero code.");
        var data = json.RootElement.GetProperty("data");
        var ownerMid = data.GetProperty("owner").GetProperty("mid").ToString();
        if (ownerMid != OfficialMid) throw new InvalidDataException("Video owner does not match the verified NTE official account.");
        var title = Text(data, "title"); var description = Text(data, "desc");
        var published = data.TryGetProperty("pubdate", out var p) ? DateTimeOffset.FromUnixTimeSeconds(p.GetInt64()) : (DateTimeOffset?)null;
        DateTimeOffset? normalized = published is null ? null : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(published.Value, "China Standard Time");
        return new GameCandidate(bvid, "NTE", "VIDEO", title, published?.ToString("O"), "UTC", normalized,
            [new CollectedEvidence("NteOfficialBilibili", "official-video", $"https://www.bilibili.com/video/{bvid}", string.IsNullOrWhiteSpace(description) ? title : description, DateTimeOffset.UtcNow, PublishedAt: published, OriginalTimezone: "UTC", NormalizedTime: normalized)]);
    }

    public async Task<SourceFetchResult> CollectAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(FeedUrl, cancellationToken);
        if ((int)response.StatusCode is 403 or 412 or 429) return new(SourceFetchStatus.Blocked, $"Bilibili returned {(int)response.StatusCode}; official account feed needs retry or user-authorized session.", []);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var code = json.RootElement.TryGetProperty("code", out var codeNode) ? codeNode.GetInt32() : -1;
        if (code != 0) return new(code is -799 or -412 ? SourceFetchStatus.Blocked : SourceFetchStatus.Failed, $"Bilibili API code {code}.", []);
        var fetched = DateTimeOffset.UtcNow;
        var videos = Objects(json.RootElement).Where(x => x.TryGetProperty("bvid", out _) && x.TryGetProperty("title", out _)).GroupBy(x => Text(x, "bvid")).Select(x => x.First());
        var candidates = videos.Select(x =>
        {
            var bvid = Text(x, "bvid"); var title = Text(x, "title"); var published = x.TryGetProperty("created", out var created) ? DateTimeOffset.FromUnixTimeSeconds(created.GetInt64()) : (DateTimeOffset?)null;
            DateTimeOffset? normalized = published is null ? null : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(published.Value, "China Standard Time");
            return new GameCandidate(bvid, "NTE", "VIDEO", title, published?.ToString("O"), "UTC", normalized, [new CollectedEvidence("NteOfficialBilibili", "official-video", $"https://www.bilibili.com/video/{bvid}", title, fetched, PublishedAt: published, OriginalTimezone: "UTC", NormalizedTime: normalized)]);
        }).ToList();
        return new(SourceFetchStatus.Healthy, $"Fetched {candidates.Count} official videos.", candidates);
    }
    private static string Text(JsonElement e,string p)=>e.TryGetProperty(p,out var v)?v.GetString()??string.Empty:string.Empty;
    private static IEnumerable<JsonElement> Objects(JsonElement e){if(e.ValueKind==JsonValueKind.Object){yield return e;foreach(var p in e.EnumerateObject())foreach(var x in Objects(p.Value))yield return x;}else if(e.ValueKind==JsonValueKind.Array)foreach(var c in e.EnumerateArray())foreach(var x in Objects(c))yield return x;}
}
