using System.Net;
using System.Xml.Linq;

namespace QimiaoDaily.Collectors;

public sealed class OfficialYoutubeRssProvider(HttpClient client)
{
    public const string GenshinChannelId = "UCiS882YPwZt1NfaM0gR0D9Q";
    public const string StarRailChannelId = "UC2PeMPA8PAOp-bynLoCeMLA";
    private const int MaxAttempts = 3;

    public async Task<IReadOnlyList<GameCandidate>> CollectAsync(string gameCode, string channelId, string expectedChannel, CancellationToken cancellationToken = default)
    {
        var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelId)}";
        var xml = await GetFeedAsync(url, cancellationToken);
        var document = XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
        var channel = document.Root?.Element(atom + "title")?.Value ?? "";
        if (!string.Equals(channel, expectedChannel, StringComparison.Ordinal))
            throw new InvalidDataException("YouTube RSS channel title does not match the configured official channel.");

        var now = DateTimeOffset.UtcNow;
        return document.Root!.Elements(atom + "entry").Select(entry =>
        {
            var id = entry.Element(yt + "videoId")?.Value ?? throw new InvalidDataException("YouTube RSS entry has no video id.");
            var title = entry.Element(atom + "title")?.Value ?? "";
            var published = DateTimeOffset.TryParse(entry.Element(atom + "published")?.Value, out var parsed) ? parsed : (DateTimeOffset?)null;
            DateTimeOffset? normalized = published is null ? null : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(published.Value, "China Standard Time");
            return new GameCandidate(id, gameCode, "VIDEO", title, published?.ToString("O"), "UTC", normalized,
                [new CollectedEvidence("OfficialYouTube", "official-video-rss", $"https://www.youtube.com/watch?v={id}", title, now,
                    PublishedAt: published, OriginalTimezone: "UTC", NormalizedTime: normalized)]);
        }).ToList();
    }

    private async Task<string> GetFeedAsync(string url, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(cancellationToken);

                if (!ShouldRetry(response.StatusCode) || attempt == MaxAttempts)
                {
                    throw new HttpRequestException("YouTube RSS request failed.", null, response.StatusCode);
                }

                await DelayBeforeRetryAsync(response, attempt, cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                last = ex;
                if (attempt == MaxAttempts) break;
                await Task.Delay(Backoff(attempt), cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode is { } statusCode && !ShouldRetry(statusCode)) throw;
                last = ex;
                if (attempt == MaxAttempts) break;
                await Task.Delay(Backoff(attempt), cancellationToken);
            }
        }

        throw new HttpRequestException($"YouTube RSS request failed after {MaxAttempts} attempts.", last);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429 || (int)statusCode >= 500;

    private static async Task DelayBeforeRetryAsync(HttpResponseMessage response, int attempt, CancellationToken cancellationToken)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        await Task.Delay(retryAfter is { } delay && delay <= TimeSpan.FromSeconds(10) ? delay : Backoff(attempt), cancellationToken);
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(Math.Min(2000, 250 * Math.Pow(2, attempt - 1)));
}
