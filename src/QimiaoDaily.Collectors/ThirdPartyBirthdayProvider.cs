using QimiaoDaily.Core;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace QimiaoDaily.Collectors;

public sealed record BirthdaySource(
    string Character,
    string Franchise,
    int Month,
    int Day,
    string Provider,
    string SourceUrl,
    string? CanonicalCharacterNameZhCn = null,
    string? EvidenceExcerpt = null);

public sealed record MergedBirthdayCandidate(
    string CanonicalCharacterNameZhCn,
    string Franchise,
    int Month,
    int Day,
    IReadOnlyList<BirthdaySource> Sources,
    string Evidence,
    VerificationStatus VerificationStatus);

public sealed class ThirdPartyBirthdayProvider(HttpClient client)
{
    public const string NteFandomApiUrl = "https://neverness-to-everness.fandom.com/api.php";
    public const string NteFandomCharacterCategoryUrl = NteFandomApiUrl + "?action=query&list=categorymembers&cmtitle=Category%3ACharacters&cmlimit=500&format=json";
    private static readonly IReadOnlyDictionary<string, string> CanonicalAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Thoma"] = "托马",
        ["Beidou"] = "北斗",
        ["Kiana Kaslana"] = "琪亚娜",
        ["Kiana"] = "琪亚娜"
    };

    public static MergedBirthdayCandidate Merge(IEnumerable<BirthdaySource> sources)
    {
        var list = sources?.Where(x => !string.IsNullOrWhiteSpace(x.Character) && !string.IsNullOrWhiteSpace(x.Franchise)).ToArray()
            ?? throw new ArgumentNullException(nameof(sources));
        if (list.Length == 0) throw new ArgumentException("At least one birthday source is required.", nameof(sources));

        var canonical = ResolveCanonical(list[0]);
        var datedSources = list.Where(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31).ToArray();
        var validDates = datedSources.Select(x => (x.Month, x.Day)).Distinct().ToArray();
        var hasOneKnownDate = validDates.Length == 1;
        var sameDate = hasOneKnownDate && datedSources.Length >= 2;
        var hasConflict = validDates.Length > 1;
        var status = sameDate ? VerificationStatus.VerifiedMultiSource : VerificationStatus.Unverified;
        var evidence = sameDate
            ? $"{datedSources.Length} 个有日期的第三方来源一致：{list[0].Month}月{list[0].Day}日；" + string.Join("；", list.Select(x => $"{x.Provider}：{x.EvidenceExcerpt ?? $"{x.Month}/{x.Day}"}"))
            : (hasConflict ? "第三方来源日期冲突，进入审核：" : "部分第三方来源缺失日期，进入审核：") + string.Join("；", list.Select(x => $"{x.Provider}={x.Month}/{x.Day} {x.EvidenceExcerpt}"));
        var known = hasOneKnownDate && !hasConflict ? validDates[0] : (0, 0);
        return new(canonical, list[0].Franchise, known.Item1, known.Item2, list, evidence, status);
    }

    public static IReadOnlyList<MergedBirthdayCandidate> MergeByCanonicalCharacter(IEnumerable<BirthdaySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Character) && !string.IsNullOrWhiteSpace(source.Franchise))
            .GroupBy(ResolveCanonical, StringComparer.OrdinalIgnoreCase)
            .Select(Merge)
            .OrderBy(candidate => candidate.CanonicalCharacterNameZhCn, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveCanonical(BirthdaySource source)
    {
        if (!string.IsNullOrWhiteSpace(source.CanonicalCharacterNameZhCn)) return source.CanonicalCharacterNameZhCn.Trim();
        if (CanonicalAliases.TryGetValue(source.Character.Trim(), out var canonical)) return canonical;
        return source.Character.Trim();
    }

    public async Task<IReadOnlyList<MergedBirthdayCandidate>> CollectAsync(Uri endpoint, string franchise, string provider, CancellationToken cancellationToken = default)
    {
        if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
        if (string.IsNullOrWhiteSpace(franchise)) throw new ArgumentException("Franchise is required.", nameof(franchise));
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider is required.", nameof(provider));
        using var document = JsonDocument.Parse(await client.GetStringAsync(endpoint, cancellationToken));
        var sources = Objects(document.RootElement)
            .Select(x => ToSource(x, franchise, provider, endpoint.ToString()))
            .Where(x => x is not null)
            .Cast<BirthdaySource>()
            .ToArray();
        return MergeByCanonicalCharacter(sources);
    }

    /// <summary>
    /// Reads the public NTE Fandom character category and each page's raw
    /// Character Infobox. Missing birthday fields are retained as UNKNOWN so
    /// the NTE coverage is visible without inventing a date.
    /// </summary>
    public async Task<IReadOnlyList<MergedBirthdayCandidate>> CollectFandomCharactersAsync(CancellationToken cancellationToken = default)
    {
        using var categoryDocument = JsonDocument.Parse(await client.GetStringAsync(NteFandomCharacterCategoryUrl, cancellationToken));
        if (!categoryDocument.RootElement.TryGetProperty("query", out var query) || !query.TryGetProperty("categorymembers", out var members) || members.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("NTE Fandom character category did not return members.");

        var titles = members.EnumerateArray().Take(100)
            .Select(member => member.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String ? titleElement.GetString()?.Trim() : null)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .ToArray();
        var results = new ConcurrentBag<MergedBirthdayCandidate>();
        using var gate = new SemaphoreSlim(8, 8);
        var tasks = titles.Select(async title =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
            var url = NteFandomApiUrl + "?action=query&prop=revisions&titles=" + Uri.EscapeDataString(title) + "&rvprop=content&rvslots=main&format=json";
            string sourceText;
            try
            {
                using var pageDocument = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
                sourceText = FindPageContent(pageDocument.RootElement) ?? string.Empty;
            }
            catch (HttpRequestException) { sourceText = string.Empty; }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { sourceText = string.Empty; }
            var birthday = ParseBirthday(sourceText);
            var source = new BirthdaySource(title, "NTE", birthday.Month, birthday.Day, "NTE Fandom Character Infobox", "https://neverness-to-everness.fandom.com/wiki/" + Uri.EscapeDataString(title));
            results.Add(Merge([source]));
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();
        await Task.WhenAll(tasks);

        return results.OrderBy(x => x.CanonicalCharacterNameZhCn, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static BirthdaySource? ToSource(JsonElement element, string franchise, string provider, string sourceUrl)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var name = Text(element, "canonicalZhCn") ?? Text(element, "character") ?? Text(element, "name") ?? Text(element, "title");
        var birthday = Text(element, "birthday") ?? Text(element, "birth");
        var month = Int(element, "month");
        var day = Int(element, "day");
        if ((month is null || day is null) && !string.IsNullOrWhiteSpace(birthday))
        {
            var match = Regex.Match(birthday, @"(?<m>\d{1,2})\s*(?:月|/|-)\s*(?<d>\d{1,2})");
            if (match.Success) { month = int.Parse(match.Groups["m"].Value); day = int.Parse(match.Groups["d"].Value); }
        }
        if (string.IsNullOrWhiteSpace(name) || month is null || day is null) return null;
        var canonical = Text(element, "canonicalZhCn");
        var url = Text(element, "sourceUrl") ?? sourceUrl;
        return new BirthdaySource(name, franchise, month.Value, day.Value, provider, url, canonical);
    }

    private static string? Text(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Int(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static IEnumerable<JsonElement> Objects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
                foreach (var child in Objects(property.Value)) yield return child;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                foreach (var nested in Objects(child)) yield return nested;
        }
    }

    private static string? FindPageContent(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } text) return text;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var found = FindPageContent(property.Value);
                if (found is not null && found.Contains("birthday", StringComparison.OrdinalIgnoreCase)) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var found = FindPageContent(child);
                if (found is not null && found.Contains("birthday", StringComparison.OrdinalIgnoreCase)) return found;
            }
        }
        return null;
    }

    private static (int Month, int Day) ParseBirthday(string source)
    {
        var match = Regex.Match(source, @"(?im)^\s*\|\s*birthday\s*=\s*(?<value>[^\r\n]+)");
        if (!match.Success) return (0, 0);
        var value = match.Groups["value"].Value.Trim();
        var numeric = Regex.Match(value, @"(?<m>\d{1,2})\s*(?:月|/|-)\s*(?<d>\d{1,2})");
        if (numeric.Success) return (int.Parse(numeric.Groups["m"].Value), int.Parse(numeric.Groups["d"].Value));
        var named = Regex.Match(value, @"(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2})", RegexOptions.IgnoreCase);
        if (!named.Success || !int.TryParse(named.Groups["day"].Value, out var day)) return (0, 0);
        var month = DateTime.ParseExact(named.Groups["month"].Value, "MMMM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None).Month;
        return (month, day);
    }
}
