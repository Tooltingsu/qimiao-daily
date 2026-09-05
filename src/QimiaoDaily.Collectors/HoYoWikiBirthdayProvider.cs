using System.Text.Json;
using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

public sealed record OfficialBirthdayCandidate(string Character, string Franchise, int Month, int Day, string Source, string SourceUrl, string Evidence, DateTimeOffset FetchedAt, bool IsUnknown = false);

/// <summary>Reads the structured base-info birthday field from HoYoLAB's official HoYoWiki API.</summary>
public sealed class HoYoWikiBirthdayProvider(HttpClient client)
{
    public const string EntryPageUrl = "https://sg-wiki-api.hoyolab.com/hoyowiki/wapi/entry_page?entry_page_id=";

    public async Task<OfficialBirthdayCandidate> CollectAsync(int entryPageId, string franchise = "GENSHIN", CancellationToken cancellationToken = default)
    {
        if (entryPageId <= 0) throw new ArgumentOutOfRangeException(nameof(entryPageId));
        var sourceUrl = EntryPageUrl + entryPageId;
        using var response = await client.GetAsync(sourceUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("retcode", out var code) || code.GetInt32() != 0)
            throw new InvalidDataException("HoYoWiki did not return a successful official response.");
        var character = FindPageName(root);
        var birthday = FindBirthday(root);
        if (string.IsNullOrWhiteSpace(character))
            throw new InvalidDataException("HoYoWiki page does not contain a structured character name.");
        if (IsOfficialUnknownBirthday(birthday))
            return new OfficialBirthdayCandidate(character, franchise, 0, 0, "HoYoWikiOfficial", sourceUrl,
                birthday is null ? "Birthday field unavailable; UNKNOWN" : "Birthday: " + birthday + "; UNKNOWN",
                DateTimeOffset.UtcNow, true);
        var parts = birthday!.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var day) || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month))
            throw new InvalidDataException("HoYoWiki birthday has an unsupported format: " + birthday);
        return new OfficialBirthdayCandidate(character, franchise, month, day, "HoYoWikiOfficial", sourceUrl, "Birthday: " + birthday, DateTimeOffset.UtcNow);
    }

    private static bool IsOfficialUnknownBirthday(string? birthday)
    {
        if (string.IsNullOrWhiteSpace(birthday)) return true;
        var text = Regex.Replace(birthday, "<[^>]+>", string.Empty).Trim();
        return text == "-";
    }

    private static string? FindPageName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("page", out var page) && page.ValueKind == JsonValueKind.Object && page.TryGetProperty("name", out var name)) return name.GetString();
            foreach (var property in element.EnumerateObject()) { var found = FindPageName(property.Value); if (found is not null) return found; }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) { var found = FindPageName(item); if (found is not null) return found; }
        return null;
    }

    private static string? FindBirthday(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var embedded = element.GetString();
            if (!string.IsNullOrWhiteSpace(embedded)) try { using var json = JsonDocument.Parse(embedded); return FindBirthday(json.RootElement); } catch (JsonException) { }
            return null;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("key", out var key) && string.Equals(key.GetString(), "Birthday", StringComparison.OrdinalIgnoreCase) && element.TryGetProperty("value", out var value))
            {
                if (value.ValueKind != JsonValueKind.Array) return value.GetString();
                var first = value.EnumerateArray().FirstOrDefault();
                return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
            }
            foreach (var property in element.EnumerateObject()) { var found = FindBirthday(property.Value); if (found is not null) return found; }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) { var found = FindBirthday(item); if (found is not null) return found; }
        return null;
    }
}
