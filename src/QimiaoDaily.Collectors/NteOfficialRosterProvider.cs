using System.Text.RegularExpressions;

namespace QimiaoDaily.Collectors;

/// <summary>
/// Reads the official NTE character slots. The official page currently does
/// not publish birthday fields, so every returned candidate is an explicit
/// UNKNOWN and remains disabled until an independently verified date exists.
/// </summary>
public sealed class NteOfficialRosterProvider(HttpClient client)
{
    public const string MainPageUrl = "https://nte.perfectworld.com/cn/main.html";

    private static readonly string[] OfficialSlots =
    [
        "yi", "zhen", "ka", "an", "xun", "zero-male", "zero-female", "mint",
        "nanally", "xiaozhi", "jiuyuan", "hasuoer", "baicang", "fadia", "dfde", "zaowu"
    ];

    public bool UsedAuditedFallback { get; private set; }

    public async Task<IReadOnlyList<OfficialBirthdayCandidate>> CollectAsync(CancellationToken cancellationToken = default)
    {
        string? html = null;
        try
        {
            using var response = await client.GetAsync(MainPageUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            // Keep the last audited official slot set available during an outage.
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout must not erase the official roster from the local DB.
        }

        var slots = ParseSlots(html);
        UsedAuditedFallback = slots.Count != OfficialSlots.Length;
        if (UsedAuditedFallback) slots = OfficialSlots;
        return slots.Select((slot, index) => CreateUnknown(slot, index + 1)).ToArray();
    }

    public static IReadOnlyList<string> ParseSlots(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        return OfficialSlots
            .Where(slot => Regex.IsMatch(html, $"(?<![A-Za-z0-9_-]){Regex.Escape(slot)}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant))
            .ToArray();
    }

    private static OfficialBirthdayCandidate CreateUnknown(string slot, int index) =>
        new($"官方角色槽位 {index:00}", "NTE", 0, 0, "NteOfficialRoster", MainPageUrl,
            $"Official NTE roster slot: {slot}; birthday field unavailable; UNKNOWN.", DateTimeOffset.UtcNow, IsUnknown: true);
}
