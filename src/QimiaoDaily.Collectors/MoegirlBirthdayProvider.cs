using System.Net;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

/// <summary>
/// Reads the public Moegirl HI3 character pages. This is a third-party
/// evidence source; missing or inaccessible birthday fields remain UNKNOWN.
/// </summary>
public sealed class MoegirlBirthdayProvider(HttpClient client)
{
    public const string BaseUrl = "https://mzh.moegirl.org.cn/";
    private const string PageSuffix = "(崩坏3)";
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(1200);
    private static readonly Regex BirthdayPattern = new(
        @"生日\s*(?:(?<year>\d{4})\s*年\s*)?(?<month>\d{1,2})\s*月\s*(?<day>\d{1,2})\s*日",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ScriptOrStyle = new("<(?:script|style)[^>]*>[\\s\\S]*?</(?:script|style)>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<BirthdaySource>> CollectAsync(IEnumerable<string> characters, CancellationToken cancellationToken = default)
    {
        var names = characters?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? throw new ArgumentNullException(nameof(characters));
        var rows = new List<BirthdaySource>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index];
            var canonical = BirthdayCharacterNameMap.Resolve(name);
            var title = canonical.EndsWith(PageSuffix, StringComparison.Ordinal) ? canonical : canonical + PageSuffix;
            var url = BaseUrl + Uri.EscapeDataString(title);
            var result = await FetchAsync(url, cancellationToken);
            rows.Add(new BirthdaySource(name, "HI3", result.Month, result.Day, "Moegirl HI3 Wiki", url, canonical, result.Evidence));
            if (index < names.Length - 1) await Task.Delay(RequestDelay, cancellationToken);
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
            if (!response.IsSuccessStatusCode)
                return (0, 0, $"HTTP {(int)response.StatusCode}; Moegirl birthday unavailable");

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = NormalizeHtml(html);
            var match = BirthdayPattern.Match(text);
            if (!match.Success || !int.TryParse(match.Groups["month"].Value, out var month) || !int.TryParse(match.Groups["day"].Value, out var day) || month is < 1 or > 12 || day is < 1 or > 31)
                return (0, 0, "Moegirl birthday field unavailable; UNKNOWN");

            var excerptStart = Math.Max(0, match.Index - 24);
            var excerptLength = Math.Min(text.Length - excerptStart, Math.Max(match.Length + 24, 80));
            var excerpt = text.Substring(excerptStart, excerptLength).Trim();
            return (month, day, $"Moegirl birthday field: {excerpt}");
        }
        catch (HttpRequestException ex) { return (0, 0, $"Moegirl request failed: {ex.Message}"); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return (0, 0, "Moegirl request timed out; UNKNOWN"); }
    }

    private static string NormalizeHtml(string html)
    {
        var withoutScripts = ScriptOrStyle.Replace(html, " ");
        var text = HtmlTag.Replace(withoutScripts, " ");
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }
}
