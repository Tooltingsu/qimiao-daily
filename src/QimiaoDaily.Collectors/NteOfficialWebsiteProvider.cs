using System.Text.Json;
using System.Text.RegularExpressions;
using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors;

/// Direct parser for the NTE official website's published news index.
/// The index is a first-party JavaScript data file, not a search-engine result.
public sealed class NteOfficialWebsiteProvider(HttpClient client)
{
    public const string NewsDataUrl = "https://nte.perfectworld.com/include/newsData20260112.js";
    public const string MainPageUrl = "https://nte.perfectworld.com/cn/main.html";
    public const string WebsiteBaseUrl = "https://nte.perfectworld.com";

    private static readonly Regex EntryPattern = new(
        "\\{\\s*\\\"title\\\":\\\"(?<title>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"url\\\":\\\"(?<url>[^\\\"]+)\\\"\\s*,\\s*\\\"time\\\":\\\"(?<time>[^\\\"]+)\\\"\\s*,\\s*\\\"channelDescription\\\":\\\"(?<description>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"channelName\\\":\\\"(?<channel>[^\\\"]+)\\\"\\s*\\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<SourceFetchResult> CollectAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        using var response = await client.GetAsync(NewsDataUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawEntries = ExtractChineseSection(source)
            .Select(match => new
            {
                Title = Decode(match.Groups["title"].Value),
                Url = match.Groups["url"].Value,
                Time = match.Groups["time"].Value,
                Description = Decode(match.Groups["description"].Value),
                Channel = match.Groups["channel"].Value
            })
            .ToList();
        var rejected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void Reject(string reason) => rejected[reason] = rejected.GetValueOrDefault(reason) + 1;
        var entries = rawEntries
            .Where(x =>
            {
                if (x.Channel is not ("gameevent" or "gamenews")) { Reject("unsupported_channel"); return false; }
                if (string.IsNullOrWhiteSpace(x.Title)) { Reject("missing_title"); return false; }
                if (!Uri.TryCreate(WebsiteBaseUrl + x.Url, UriKind.Absolute, out _)) { Reject("invalid_url"); return false; }
                return true;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Title) && Uri.TryCreate(WebsiteBaseUrl + x.Url, UriKind.Absolute, out _))
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(limit)
            .ToList();
        var duplicateCount = rawEntries.Count(x => x.Channel is "gameevent" or "gamenews" && !string.IsNullOrWhiteSpace(x.Title) && Uri.TryCreate(WebsiteBaseUrl + x.Url, UriKind.Absolute, out _)) - entries.Count;
        if (duplicateCount > 0) rejected["duplicate_url_or_limit"] = duplicateCount;

        var fetched = DateTimeOffset.UtcNow;
        var candidates = new List<GameCandidate>();
        foreach (var entry in entries)
        {
            var url = WebsiteBaseUrl + entry.Url;
            var detail = await TryFetchDetailAsync(url, cancellationToken);
            var detailText = string.IsNullOrWhiteSpace(detail) ? entry.Description : detail;
            var window = NteActivityTimeParser.Parse(detailText, entry.Time);
            var published = window.PublishedAt;
            var text = string.IsNullOrWhiteSpace(detailText) ? entry.Title : entry.Title + " - " + detailText;
            var sourceTime = window.StartExpression is null ? entry.Time : string.Join("-", new[] { window.StartExpression, window.EndExpression }.Where(x => !string.IsNullOrWhiteSpace(x)));
            candidates.Add(new GameCandidate(
                "nte-web-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))).ToLowerInvariant()[..16],
                "NTE", "EVENT", entry.Title, sourceTime, "Asia/Shanghai", window.Start,
                [new CollectedEvidence("NteOfficialWebsite", "official-news-index", url, text, fetched, entry.Title, published, "Asia/Shanghai", window.Start)], window.End,
                window.Precision, window.End is null ? TimePrecision.Relative : window.Precision == TimePrecision.Relative ? TimePrecision.Exact : window.Precision,
                window.StartSource, window.EndSource, window.StartExpression, window.EndExpression,
                entry.Url + ":start", entry.Url + ":end") { SourceCandidateCount = rawEntries.Count, SourceRejectedCount = rejected.Values.Sum(), SourceRejectionReasons = new Dictionary<string, int>(rejected, StringComparer.OrdinalIgnoreCase) });
        }

        return new(SourceFetchStatus.Healthy, $"Fetched {candidates.Count} official NTE website entries.", candidates, rawEntries.Count, rejected.Values.Sum(), new Dictionary<string, int>(rejected, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<string?> TryFetchDetailAsync(string url, CancellationToken cancellationToken)
    {
        try { return await client.GetStringAsync(url, cancellationToken); }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    public async Task<IReadOnlyList<GameCandidate>> CollectVideosAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        using var response = await client.GetAsync(MainPageUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadAsStringAsync(cancellationToken);
        var fetched = DateTimeOffset.UtcNow;
        var urls = Regex.Matches(source, "https://ntevmg\\.perfectworld\\.com/[^\\\"'<> ]+\\.mp4", RegexOptions.IgnoreCase)
            .Cast<Match>().Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
        return urls.Select(url =>
        {
            var name = Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);
            return new GameCandidate(
                "nte-video-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))).ToLowerInvariant()[..16],
                "NTE", "VIDEO", name, null, null, null,
                [new CollectedEvidence("NteOfficialWebsite", "official-video", url, "Official NTE website video asset: " + name, fetched, name)])
            { SourceCandidateCount = urls.Count };
        }).ToList();
    }

    private static IEnumerable<Match> ExtractChineseSection(string source)
    {
        var start = source.IndexOf("\"cn\"", StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException("NTE official news data has no Chinese section.");
        var end = source.IndexOf("\"de\"", start, StringComparison.Ordinal);
        var section = end > start ? source[start..end] : source[start..];
        return EntryPattern.Matches(section).Cast<Match>();
    }

    private static string Decode(string value)
    {
        try { return JsonSerializer.Deserialize<string>($"\"{value.Replace("\\\"", "\\\\\"")}\"") ?? value; }
        catch { return value.Replace("\\n", " ").Replace("\\\"", "\""); }
    }
}
