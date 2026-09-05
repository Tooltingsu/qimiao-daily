using System.Net;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

public sealed class BiligameBirthdayProvider(HttpClient client)
{
    public const string BaseUrl = "https://wiki.biligame.com/bh3/index.php?title=";
    private static readonly Regex BirthdayCell = new("<th[^>]*>\\s*生日\\s*</th>\\s*<td[^>]*>(?<value>.*?)</td>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new("(?<month>\\d{1,2})\\s*月\\s*(?<day>\\d{1,2})\\s*日?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";
    private const int MaxTransientAttempts = 3;

    public async Task<IReadOnlyList<BirthdaySource>> CollectAsync(IEnumerable<string> characters, CancellationToken cancellationToken = default)
    {
        var names = characters?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? throw new ArgumentNullException(nameof(characters));
        var rows = new List<BirthdaySource>(names.Length);
        foreach (var name in names)
        {
            var canonical = BirthdayCharacterNameMap.Resolve(name);
            var url = BaseUrl + Uri.EscapeDataString(canonical);
            var (month, day, excerpt) = await FetchAsync(url, cancellationToken);
            rows.Add(new BirthdaySource(name, "HI3", month, day, "Biligame HI3 Wiki", url, canonical, excerpt));
        }
        return rows;
    }

    private async Task<(int Month, int Day, string Evidence)> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
                request.Headers.Referrer = new Uri("https://wiki.biligame.com/bh3/");
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < MaxTransientAttempts && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                        continue;
                    }

                    return (0, 0, $"HTTP {(int)response.StatusCode}; birthday unavailable");
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var cell = BirthdayCell.Match(html);
                var value = cell.Success ? WebUtility.HtmlDecode(Regex.Replace(cell.Groups["value"].Value, "<[^>]+>", " ")).Trim() : string.Empty;
                var match = DatePattern.Match(value);
                return match.Success && int.TryParse(match.Groups["month"].Value, out var month) && int.TryParse(match.Groups["day"].Value, out var day)
                    ? (month, day, $"Biligame birthday field: {value}")
                    : (0, 0, string.IsNullOrWhiteSpace(value) ? "Biligame birthday field unavailable; UNKNOWN" : $"Biligame birthday field: {value}; UNKNOWN");
            }

            return (0, 0, "Biligame transient response exhausted; UNKNOWN");
        }
        catch (HttpRequestException ex) { return (0, 0, $"Biligame request failed: {ex.Message}"); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return (0, 0, "Biligame request timed out; UNKNOWN"); }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
