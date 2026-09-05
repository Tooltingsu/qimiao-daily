using QimiaoDaily.Collectors;
using QimiaoDaily.Data;
using Microsoft.EntityFrameworkCore;

namespace QimiaoDaily.Services;

public sealed record ArtworkRefreshOutcome(ArtworkFetchStatus Status, string Message, int Imported, int Target, int Fetched);
public sealed class ArtworkDailyRefreshService(PixivArtworkProvider provider, ArtworkImportService importer, QimiaoDailyDbContext? database = null)
{
    public async Task<ArtworkRefreshOutcome> RefreshAsync(
        int target = 30,
        int rankingLimit = 30,
        IReadOnlyList<string>? directArtworkIds = null,
        IReadOnlyList<ArtworkSearchRequest>? characterSearches = null,
        int resultsPerCharacter = 3,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var imported = 0;
        var fetched = 0;
        try
        {
            ArtworkFetchStatus sourceStatus;
            string sourceMessage;
            var candidates = new List<OfficialArtworkCandidate>();
            if (characterSearches is { Count: > 0 })
            {
                var messages = new List<string>();
                var failedStatus = ArtworkFetchStatus.Healthy;
                foreach (var search in characterSearches)
                {
                    var result = await provider.SearchAsync(search, resultsPerCharacter, cancellationToken);
                    messages.Add(result.Message);
                    if (result.Status == ArtworkFetchStatus.Healthy) candidates.AddRange(result.Candidates);
                    else if (failedStatus == ArtworkFetchStatus.Healthy) failedStatus = result.Status;
                }
                sourceStatus = candidates.Count > 0 ? ArtworkFetchStatus.Healthy : failedStatus;
                sourceMessage = $"Character-search catalog queried {characterSearches.Count} profiles and returned {candidates.Count} candidates. " + string.Join(" | ", messages);
            }
            else
            {
                var result = await provider.FetchDailyRankingAsync(rankingLimit, cancellationToken);
                sourceStatus = result.Status;
                sourceMessage = result.Message;
                if (result.Status == ArtworkFetchStatus.Healthy) candidates.AddRange(result.Candidates);
            }
            foreach (var artworkId in directArtworkIds ?? [])
            {
                var direct = await provider.FetchAsync(artworkId, cancellationToken);
                if (direct.Status == ArtworkFetchStatus.Healthy && direct.Candidate is not null)
                    candidates.Add(direct.Candidate);
            }

            fetched = candidates.Count;
            if (candidates.Count == 0)
            {
                await RecordRunAsync(target, fetched, imported, StatusText(sourceStatus), sourceMessage, started, cancellationToken);
                return new(sourceStatus, sourceMessage, imported, target, fetched);
            }

            foreach (var candidate in candidates
                .GroupBy(candidate => candidate.Platform + ":" + candidate.ArtworkId, StringComparer.Ordinal)
                .Select(group => group.First()))
            {
                if (imported >= target) break;
                if (!await importer.ImportAsync(candidate, cancellationToken: cancellationToken)) continue;
                imported++;
            }

            sourceMessage = sourceStatus == ArtworkFetchStatus.Healthy
                ? sourceMessage
                : $"Pixiv source was unavailable ({sourceMessage}); configured direct artwork IDs were used.";
            var message = imported >= target
                ? sourceMessage
                : $"{sourceMessage} Only {imported}/{target} truly new candidates were imported; old artwork was not reused.";
            await RecordRunAsync(target, fetched, imported, imported >= target ? "HEALTHY" : "PARTIAL", message, started, cancellationToken);
            return new(ArtworkFetchStatus.Healthy, message, imported, target, fetched);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordRunAsync(target, fetched, imported, "FAILED", ex.Message, started, CancellationToken.None);
            throw;
        }
    }

    private static string StatusText(ArtworkFetchStatus status) => status switch
    {
        ArtworkFetchStatus.LoginRequired => "LOGIN_REQUIRED",
        ArtworkFetchStatus.Blocked => "BLOCKED",
        ArtworkFetchStatus.Failed => "FAILED",
        _ => "HEALTHY"
    };

    private async Task RecordRunAsync(int target, int fetched, int imported, string status, string? reason, DateTimeOffset started, CancellationToken cancellationToken)
    {
        if (database is null) return;
        database.ArtworkDailyRuns.Add(new ArtworkDailyRunEntity
        {
            Provider = "Pixiv",
            TargetCount = target,
            FetchedCount = fetched,
            NewCandidateCount = imported,
            Status = status,
            FailureReason = reason,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
