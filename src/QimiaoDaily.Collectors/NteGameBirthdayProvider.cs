using System.Globalization;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

/// <summary>
/// Reads explicit birthday fields from the public NTEGame character index.
/// This is a non-official single source: callers must keep results unverified
/// and disabled until a second reliable source agrees.
/// </summary>
public sealed class NteGameBirthdayProvider(HttpClient client)
{
    public const string ListUrl = "https://www.ntegame.com/characters/";

    private static readonly Regex BirthdayPattern = new(
        @"(?:\x5c)?\x22Name(?:\x5c)?\x22\s*:\s*(?:\x5c)?\x22(?<name>[^\x22\x5c]+)(?:\x5c)?\x22[\s\S]{0,500}?(?:\x5c)?\x22Birthday(?:\x5c)?\x22\s*:\s*(?:\x5c)?\x22(?<birthday>[^\x22\x5c]+)(?:\x5c)?\x22",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<BirthdaySource>> CollectAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ListUrl);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/137.0 Safari/537.36");
        request.Headers.Referrer = new Uri("https://www.ntegame.com/");
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = new Dictionary<string, BirthdaySource>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in BirthdayPattern.Matches(html))
        {
            var name = match.Groups["name"].Value.Trim();
            var value = match.Groups["birthday"].Value.Trim();
            if (string.IsNullOrWhiteSpace(name) || !TryParse(value, out var month, out var day)) continue;
            var url = "https://www.ntegame.com/characters/" + Uri.EscapeDataString(name) + "/";
            results[name] = new BirthdaySource(name, "NTE", month, day, "NTEGame", url,
                CanonicalCharacterNameZhCn: null,
                EvidenceExcerpt: $"NTEGame public character profile birthday: {value}; non-official single source; pending second-source verification.");
        }
        return results.Values.OrderBy(x => x.Character, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool TryParse(string value, out int month, out int day)
    {
        if (DateTime.TryParseExact(value, ["MMMM d", "MMM d"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var namedDate))
        {
            month = namedDate.Month;
            day = namedDate.Day;
            return true;
        }
        var match = Regex.Match(value, "^(?<month>\\d{1,2})[/-](?<day>\\d{1,2})$", RegexOptions.CultureInvariant);
        if (!match.Success) match = Regex.Match(value, "^(?<month>\\d{1,2})月(?<day>\\d{1,2})日$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups["month"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out month) || !int.TryParse(match.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out day))
        {
            month = day = 0;
            return false;
        }
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }
}
