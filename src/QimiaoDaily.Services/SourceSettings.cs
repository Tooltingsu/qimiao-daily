using System.Text.Json;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>
/// User-maintained source preferences. Invalid or absent files intentionally fall back to
/// conservative built-in defaults so scheduled work remains usable after a bad edit.
/// </summary>
public sealed record SourceSettings(
    IReadOnlyList<string> BgiRepositories,
    int ArtworkDailyRankingLimit,
    int ArtworkTargetCount,
    IReadOnlyList<string> ArtworkIds)
{
    public const string FileName = "source_settings.json";

    public static SourceSettings Default { get; } = new(
        ["babalae/better-genshin-impact", "babalae/bettergi-scripts-list"],
        ArtworkDailyRankingLimit: 30,
        ArtworkTargetCount: 30,
        ArtworkIds: []);

    public static SourceSettings Load(QimiaoDailyPaths paths)
    {
        var file = Path.Combine(paths.ConfigDirectory, FileName);
        if (!File.Exists(file)) return Default;

        try
        {
            var input = JsonSerializer.Deserialize<SourceSettingsFile>(File.ReadAllText(file), JsonOptions);
            var repositories = input?.BgiRepositories?
                .Where(IsRepository).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var artworkIds = input?.Artwork?.DirectArtworkIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.All(char.IsAsciiDigit))
                .Distinct(StringComparer.Ordinal).ToArray();
            return new(
                repositories is { Length: > 0 } ? repositories : Default.BgiRepositories,
                Clamp(input?.Artwork?.DailyRankingLimit, Default.ArtworkDailyRankingLimit),
                Clamp(input?.Artwork?.TargetCount, Default.ArtworkTargetCount),
                artworkIds ?? []);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    private static int Clamp(int? value, int fallback) => value is >= 1 and <= 100 ? value.Value : fallback;
    private static bool IsRepository(string value) => value.Count(character => character == '/') == 1 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '/');
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record SourceSettingsFile(IReadOnlyList<string>? BgiRepositories, ArtworkSettingsFile? Artwork);
    private sealed record ArtworkSettingsFile(int? DailyRankingLimit, int? TargetCount, IReadOnlyList<string>? DirectArtworkIds);
}
