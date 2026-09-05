using System.Globalization;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

public sealed record NevernessCharacterPage(string Character, string Url);

/// <summary>Reads the public Birthdate field from audited Neverness.gg NTE pages.</summary>
public sealed class NteNevernessGgBirthdayProvider
{
    public const string SitemapUrl = "https://neverness.gg/post-sitemap.xml";
    private readonly HttpClient client;
    private readonly IReadOnlyList<NevernessCharacterPage> pages;
    private static readonly Regex BirthdatePattern = new(@"Birthdate\s*[:\-]\s*(?<value>[A-Za-z]+(?:\s+\d{1,2})?|TBA|TBD|Unknown)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LocPattern = new("<loc>\\s*(?<url>[^<]+)\\s*</loc>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public NteNevernessGgBirthdayProvider(HttpClient client, IReadOnlyList<NevernessCharacterPage>? pages = null)
    {
        this.client = client;
        this.pages = pages ?? [];
    }

    public async Task<IReadOnlyList<BirthdaySource>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var targets = pages.Count > 0 ? pages : await DiscoverPagesAsync(cancellationToken);
        var results = new List<BirthdaySource>();
        foreach (var page in targets)
        {
            string html;
            try { html = await client.GetStringAsync(page.Url, cancellationToken); }
            catch (HttpRequestException) { continue; }
            var (month, day, raw) = Parse(html);
            results.Add(new BirthdaySource(page.Character, "NTE", month, day, "NteNevernessGgBirthday", page.Url,
                EvidenceExcerpt: $"Neverness.gg public Birthdate: {raw ?? "UNKNOWN"}; third-party candidate source."));
        }
        return results;
    }

    private async Task<IReadOnlyList<NevernessCharacterPage>> DiscoverPagesAsync(CancellationToken cancellationToken)
    {
        string xml;
        try { xml = await client.GetStringAsync(SitemapUrl, cancellationToken); }
        catch (HttpRequestException) { return []; }
        return LocPattern.Matches(xml).Select(m => m.Groups["url"].Value.Trim())
            .Where(url => url.Contains("-nte-build", StringComparison.OrdinalIgnoreCase))
            .Select(url => new NevernessCharacterPage(SlugToName(url), url)).ToArray();
    }

    private static string SlugToName(string url)
    {
        var slug = new Uri(url).AbsolutePath.Trim('/').Replace("-nte-build", "", StringComparison.OrdinalIgnoreCase);
        return string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    internal static (int Month, int Day, string? Raw) Parse(string html)
    {
        var match = BirthdatePattern.Match(Regex.Replace(html, "<[^>]+>", " "));
        if (!match.Success) return (0, 0, null);
        var raw = match.Groups["value"].Value.Trim();
        if (!DateTime.TryParseExact(raw, ["MMMM d", "MMM d"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)) return (0, 0, raw);
        return (date.Month, date.Day, raw);
    }
}
