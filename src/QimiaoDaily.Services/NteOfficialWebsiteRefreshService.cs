using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class NteOfficialWebsiteRefreshService(QimiaoDailyDbContext database, NteOfficialWebsiteProvider provider)
{
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var result = await provider.CollectAsync(cancellationToken: cancellationToken);
        if (result.Status != SourceFetchStatus.Healthy) throw new InvalidOperationException(result.Message);
        var imported = 0;
        var candidates = result.Candidates.Concat(await provider.CollectVideosAsync(cancellationToken: cancellationToken));
        var importer = new TimelineCandidateImportService(database);
        foreach (var candidate in candidates.GroupBy(TimelineChangeClassifier.Identity, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
        {
            var import = await importer.ApplyCandidateAsync(candidate, "nte-official-web-v2", "nte-official-provider", "official refresh", cancellationToken);
            if (import.ChangeKind != TimelineChangeKind.None) imported++;
        }
        return imported;
    }
}
