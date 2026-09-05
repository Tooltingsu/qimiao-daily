using System.Net;
using System.Text.Json;

namespace QimiaoDaily.Collectors;

public enum ArtworkFetchStatus { Healthy, LoginRequired, Blocked, Failed }
public sealed record ArtworkFetchResult(ArtworkFetchStatus Status, string Message, OfficialArtworkCandidate? Candidate);
public sealed record ArtworkSearchRequest(string CharacterName, string FranchiseName, string Keyword);
public sealed record ArtworkSearchResult(ArtworkFetchStatus Status, string Message, IReadOnlyList<OfficialArtworkCandidate> Candidates);
public sealed record OfficialArtworkCandidate(
    string Platform,
    string ArtworkId,
    string Title,
    string Author,
    string AuthorId,
    string SourceUrl,
    string ThumbnailUrl,
    DateTimeOffset PublishedAt,
    DateTimeOffset FetchedAt,
    string? CharacterName = null,
    string? FranchiseName = null,
    string? Category = null,
    string? Tags = null,
    int? Width = null,
    int? Height = null,
    string? SourceMetadata = null);

/// <summary>Direct Pixiv artwork endpoint. A caller supplies discovered artwork ids; no search API is a core dependency.</summary>
public sealed class PixivArtworkProvider(HttpClient client, string? sessionCookie = null)
{
    public const string ArtworkUrl = "https://www.pixiv.net/artworks/";
    public const string AjaxUrl = "https://www.pixiv.net/ajax/illust/";
    public const string DailyRankingUrl = "https://www.pixiv.net/ranking.php?mode=daily&format=json";
    public const string SearchUrl = "https://www.pixiv.net/ajax/search/artworks/";

    public async Task<ArtworkSearchResult> SearchAsync(ArtworkSearchRequest search, int limit = 3, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search.Keyword)) throw new ArgumentException("Pixiv search keyword is required.", nameof(search));
        if (limit is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(limit));

        var keyword = Uri.EscapeDataString(search.Keyword.Trim());
        var url = $"{SearchUrl}{keyword}?word={keyword}&order=date_d&mode=all&p=1&s_mode=s_tag&type=all&lang=zh";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri("https://www.pixiv.net/");
        AddSession(request);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(ArtworkFetchStatus.LoginRequired, "Pixiv requires an authorized session for character search.", []);
        if ((int)response.StatusCode is 429 or 503) return new(ArtworkFetchStatus.Blocked, "Pixiv temporarily blocked or rate-limited character search.", []);
        if (!response.IsSuccessStatusCode) return new(ArtworkFetchStatus.Failed, "Pixiv returned HTTP " + (int)response.StatusCode + " for character search.", []);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("body", out var body) || !body.TryGetProperty("illustManga", out var illustManga) || !illustManga.TryGetProperty("data", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return new(ArtworkFetchStatus.Failed, "Pixiv character-search response has no illustManga data.", []);

        var fetched = DateTimeOffset.UtcNow;
        var candidates = new List<OfficialArtworkCandidate>();
        foreach (var item in entries.EnumerateArray())
        {
            var id = Text(item, "id");
            var title = Text(item, "title");
            var author = Text(item, "userName");
            var authorId = Text(item, "userId");
            var thumbnail = Text(item, "url");
            var date = Text(item, "createDate");
            var published = DateTimeOffset.TryParse(date, out var parsed) ? parsed : fetched;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(thumbnail)) continue;
            candidates.Add(new("PIXIV", id, title, author, authorId, ArtworkUrl + id, thumbnail, published, fetched,
                search.CharacterName, search.FranchiseName, Text(item, "illustType"), Tags(item), Number(item, "width"), Number(item, "height"), item.GetRawText()));
            if (candidates.Count == limit) break;
        }
        return new(ArtworkFetchStatus.Healthy, $"Fetched {candidates.Count} Pixiv candidates for {search.CharacterName}.", candidates);
    }

    public async Task<(ArtworkFetchStatus Status, string Message, IReadOnlyList<OfficialArtworkCandidate> Candidates)> FetchDailyRankingAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        using var request = new HttpRequestMessage(HttpMethod.Get, DailyRankingUrl); request.Headers.Referrer = new Uri("https://www.pixiv.net/"); AddSession(request);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return (ArtworkFetchStatus.LoginRequired, "Pixiv requires an authorized session for daily ranking.", []);
        if ((int)response.StatusCode is 429 or 503) return (ArtworkFetchStatus.Blocked, "Pixiv temporarily blocked or rate-limited daily ranking.", []);
        if (!response.IsSuccessStatusCode) return (ArtworkFetchStatus.Failed, "Pixiv returned HTTP " + (int)response.StatusCode + ".", []);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("contents", out var entries) || entries.ValueKind != JsonValueKind.Array) return (ArtworkFetchStatus.Failed, "Pixiv ranking response has no contents array.", []);
        var fetched = DateTimeOffset.UtcNow; var list = new List<OfficialArtworkCandidate>();
        foreach (var item in entries.EnumerateArray())
        {
            var id = Text(item, "illust_id"); var title = Text(item, "title"); var author = Text(item, "user_name"); var authorId = Text(item, "user_id"); var thumbnail = Text(item, "url");
            var published = item.TryGetProperty("illust_upload_timestamp", out var timestamp) && timestamp.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : fetched;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(thumbnail)) continue;
            list.Add(new("PIXIV", id, title, author, authorId, ArtworkUrl + id, thumbnail, published, fetched,
                Category: Text(item, "type"), Tags: Tags(item), Width: Number(item, "width"), Height: Number(item, "height"), SourceMetadata: item.GetRawText())); if (list.Count == limit) break;
        }
        return (ArtworkFetchStatus.Healthy, "Fetched " + list.Count + " direct Pixiv daily-ranking candidates.", list);
    }

    public async Task<ArtworkFetchResult> FetchAsync(string artworkId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artworkId) || !artworkId.All(char.IsAsciiDigit)) throw new ArgumentException("Pixiv artwork id must be numeric.", nameof(artworkId));
        using var request = new HttpRequestMessage(HttpMethod.Get, AjaxUrl + artworkId);
        request.Headers.Referrer = new Uri("https://www.pixiv.net/"); AddSession(request);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(ArtworkFetchStatus.LoginRequired, "Pixiv requires an authorized session for this artwork.", null);
        if ((int)response.StatusCode is 429 or 503) return new(ArtworkFetchStatus.Blocked, "Pixiv temporarily blocked or rate-limited the request.", null);
        if (!response.IsSuccessStatusCode) return new(ArtworkFetchStatus.Failed, "Pixiv returned HTTP " + (int)response.StatusCode + ".", null);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error) && error.GetBoolean()) return new(ArtworkFetchStatus.Failed, "Pixiv returned an API error.", null);
        if (!root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object) return new(ArtworkFetchStatus.Failed, "Pixiv response has no artwork body.", null);
        var title = Text(body, "title"); var author = Text(body, "userName"); var authorId = Text(body, "userId");
        var thumbnail = string.Empty;
        if (body.TryGetProperty("urls", out var urls))
        {
            if (urls.TryGetProperty("thumb", out var thumb)) thumbnail = thumb.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(thumbnail) && urls.TryGetProperty("thumb_mini", out var legacyThumb)) thumbnail = legacyThumb.GetString() ?? string.Empty;
        }
        var dateText = Text(body, "createDate");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(thumbnail) || !DateTimeOffset.TryParse(dateText, out var published)) return new(ArtworkFetchStatus.Failed, "Pixiv response is missing required artwork metadata.", null);
        return new(ArtworkFetchStatus.Healthy, "Fetched official Pixiv artwork metadata.", new("PIXIV", artworkId, title, author, authorId, ArtworkUrl + artworkId, thumbnail, published, DateTimeOffset.UtcNow,
            Category: Text(body, "illustType"), Tags: Tags(body), Width: Number(body, "width"), Height: Number(body, "height"), SourceMetadata: body.GetRawText()));
    }
    private static string Text(JsonElement value, string name) => value.TryGetProperty(name, out var element) ? element.ToString() : string.Empty;
    private static int? Number(JsonElement value, string name) => value.TryGetProperty(name, out var element) && element.TryGetInt32(out var number) ? number : null;
    private static string Tags(JsonElement value)
    {
        if (!value.TryGetProperty("tags", out var tags)) return string.Empty;
        if (tags.ValueKind == JsonValueKind.Array) return string.Join(", ", tags.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("name", out var name) ? name.ToString() : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("tags", out var nested) && nested.ValueKind == JsonValueKind.Array)
            return string.Join(", ", nested.EnumerateArray().Select(x => x.TryGetProperty("name", out var name) ? name.ToString() : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        return tags.ToString();
    }

    private void AddSession(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(sessionCookie)) request.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
    }
}
