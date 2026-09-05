using System.Text.Json;

namespace QimiaoDaily.Collectors;

/// <summary>
/// Reads the official Honkai Impact 3 character index used by the official
/// site's Valkyries page. The index is a character-list source only: it does
/// not expose birthday fields, so every returned candidate remains UNKNOWN.
/// </summary>
public sealed class Honkai3OfficialCharacterProvider(HttpClient client)
{
    public const string AppId = "5fcd2aa439ca4aea";
    public const int ChannelId = 520;
    public const string ApiBase = "https://sg-public-api-static.hoyoverse.com/content_v2_user";
    public const string SourcePageUrl = "https://honkaiimpact3.hoyoverse.com/asia/en-us/valkyries";

    public async Task<IReadOnlyList<OfficialBirthdayCandidate>> CollectAsync(
        string language = "en-us", CancellationToken cancellationToken = default)
    {
        var url = $"{ApiBase}/app/{AppId}/getContentList?iChanId={ChannelId}&iPageSize=200&iPage=1&sLangKey={Uri.EscapeDataString(language)}&isPreview=0";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        if (!root.TryGetProperty("retcode", out var retcode) || retcode.GetInt32() != 0)
            throw new InvalidDataException("Honkai 3 official character API returned an error.");
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Honkai 3 official character API did not return a character list.");

        var fetched = DateTimeOffset.UtcNow;
        var results = new List<OfficialBirthdayCandidate>();
        foreach (var item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("sExt", out var extElement) || extElement.ValueKind != JsonValueKind.String)
                continue;
            using var ext = JsonDocument.Parse(extElement.GetString() ?? "{}");
            var character = ReadName(ext.RootElement);
            if (string.IsNullOrWhiteSpace(character)) continue;
            results.Add(new OfficialBirthdayCandidate(
                character,
                "HI3",
                0,
                0,
                "Honkai3OfficialCharacterList",
                url,
                $"Official character list API entry: {character}; page: {SourcePageUrl}; birthday field unavailable; UNKNOWN",
                fetched,
                IsUnknown: true));
        }

        return results
            .GroupBy(x => x.Character, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static string? ReadName(JsonElement ext)
    {
        foreach (var key in new[] { "520_0", "520_1" })
            if (ext.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!.Trim();
        return null;
    }
}
