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
        // This endpoint is protected by Bilibili's risk controls.  A bare
        // data-center HttpClient request is regularly answered with HTTP 412
        // or API -799 even though the account and feed are valid.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var request = CreateFeedRequest();
            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode is 403 or 412 or 429)
            {
                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                    continue;
                }
                return new(SourceFetchStatus.Blocked, $"Bilibili returned {(int)response.StatusCode} after browser-header retries; official account feed needs an authorized session or fallback source.", []);
            }
            if (!response.IsSuccessStatusCode)
                return new(SourceFetchStatus.Failed, $"Bilibili HTTP {(int)response.StatusCode}.", []);

            using var json = JsonDocument.Parse(payload);
            var code = json.RootElement.TryGetProperty("code", out var codeNode) ? codeNode.GetInt32() : -1;
            if (code is -799 or -412)
            {
                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                    continue;
                }
                return new(SourceFetchStatus.Blocked, $"Bilibili API code {code} after browser-header retries; official account feed needs an authorized session or fallback source.", []);
            }
            if (code != 0) return new(SourceFetchStatus.Failed, $"Bilibili API code {code}.", []);
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
        throw new InvalidOperationException("Unreachable Bilibili collection state.");
    }
    private static HttpRequestMessage CreateFeedRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140 Safari/537.36");
        request.Headers.Referrer = new Uri($"https://space.bilibili.com/{OfficialMid}/");
        request.Headers.Accept.ParseAdd("application/json, text/plain, */*");
        request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
        request.Headers.TryAddWithoutValidation("Origin", "https://space.bilibili.com");
        var session = Environment.GetEnvironmentVariable("BILIBILI_SESSDATA");
        if (!string.IsNullOrWhiteSpace(session)) request.Headers.TryAddWithoutValidation("Cookie", $"SESSDATA={session}");
        return request;
    }
    private static string Text(JsonElement e,string p)=>e.TryGetProperty(p,out var v)?v.GetString()??string.Empty:string.Empty;
    private static IEnumerable<JsonElement> Objects(JsonElement e){if(e.ValueKind==JsonValueKind.Object){yield return e;foreach(var p in e.EnumerateObject())foreach(var x in Objects(p.Value))yield return x;}else if(e.ValueKind==JsonValueKind.Array)foreach(var c in e.EnumerateArray())foreach(var x in Objects(c))yield return x;}
}
