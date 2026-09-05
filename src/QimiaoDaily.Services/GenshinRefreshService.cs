using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class GenshinRefreshService(QimiaoDailyDbContext database, GenshinAnnouncementProvider provider)
{
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default, string? itemType = null)
    {
        var candidates = await provider.CollectAsync(cancellationToken);
        var added = 0;
        var importer = new TimelineCandidateImportService(database);
        foreach (var candidate in candidates.GroupBy(TimelineChangeClassifier.Identity, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
        {
            if (itemType is not null && !string.Equals(candidate.ItemType, itemType, StringComparison.OrdinalIgnoreCase)) continue;
            var result = await importer.ApplyCandidateAsync(candidate, "genshin-announcement-v2", "genshin-provider", "official refresh", cancellationToken);
            if (result.ChangeKind != TimelineChangeKind.None) added++;
        }
        return added;
    }
}
