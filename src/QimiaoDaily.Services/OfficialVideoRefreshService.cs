using QimiaoDaily.Collectors;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>Refreshes official video feeds independently so one unavailable channel cannot hide another.</summary>
public sealed class OfficialVideoRefreshService(QimiaoDailyDbContext database, HttpClient client)
{
    private static readonly (string GameCode, string ChannelId, string ChannelName, string HealthName)[] Sources =
    [
        ("GENSHIN", OfficialYoutubeRssProvider.GenshinChannelId, "Genshin Impact", "OfficialYoutubeRSS:Genshin"),
        ("STARRAIL", OfficialYoutubeRssProvider.StarRailChannelId, "Honkai: Star Rail", "OfficialYoutubeRSS:StarRail")
    ];

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var provider = new OfficialYoutubeRssProvider(client);
        var importer = new OfficialVideoImportService(database);
        var failures = new List<Exception>();
        var imported = 0;

        foreach (var source in Sources)
        {
            var started = DateTimeOffset.UtcNow;
            try
            {
                var candidates = await provider.CollectAsync(source.GameCode, source.ChannelId, source.ChannelName, cancellationToken);
                foreach (var candidate in candidates)
                    if (await importer.ImportAsync(candidate, cancellationToken)) imported++;
                await new OperationsService(database).RecordSuccessAsync(source.HealthName, candidates.Count,
                    (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException(source.HealthName + ": " + ex.Message, ex));
                var status = ex is InvalidDataException ? "WARNING" : "FAILED";
                await new OperationsService(database).RecordFailureAsync(source.HealthName, status, ex.Message,
                    (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken: cancellationToken);
            }
        }

        if (failures.Count == Sources.Length)
            throw new AggregateException("All official video sources failed.", failures);
        return imported;
    }
}
