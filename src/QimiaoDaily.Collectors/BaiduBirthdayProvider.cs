using System.Net;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

public sealed class BaiduBirthdayProvider(HttpClient client)
{
    public const string BaseUrl = "https://baike.baidu.com/item/";
    private static readonly Regex JsonBirthday = new("\\\"key\\\":\\\"birthday\\\"[\\s\\S]{0,1800}?\\\"text\\\":\\\"(?<value>[^\\\"]+)\\\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlBirthday = new("生日.{0,300}?(?<value>\\d{1,2})月(?<day>\\d{1,2})日", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new("(?<month>\\d{1,2})\\s*月\\s*(?<day>\\d{1,2})\\s*日?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";

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
            rows.Add(new BirthdaySource(name, "HI3", month, day, "Baidu Baike", url, canonical, excerpt));
        }
        return rows;
    }

    private async Task<(int Month, int Day, string Evidence)> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return (0, 0, $"HTTP {(int)response.StatusCode}; birthday unavailable");
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = JsonBirthday.Match(html);
            var value = match.Success ? Regex.Unescape(WebUtility.HtmlDecode(match.Groups["value"].Value)) : string.Empty;
            if (!match.Success) value = HtmlBirthday.Match(html).Groups["value"].Value;
            var date = DatePattern.Match(value);
            return date.Success && int.TryParse(date.Groups["month"].Value, out var month) && int.TryParse(date.Groups["day"].Value, out var day)
                ? (month, day, $"Baidu birthday field: {value}")
                : (0, 0, string.IsNullOrWhiteSpace(value) ? "Baidu birthday field unavailable; UNKNOWN" : $"Baidu birthday field: {value}; UNKNOWN");
        }
        catch (HttpRequestException ex) { return (0, 0, $"Baidu request failed: {ex.Message}"); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return (0, 0, "Baidu request timed out; UNKNOWN"); }
    }
}
