using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed record GameCoverageResult(
    string GameCode,
    int CandidateCount,
    int ParsedCount,
    int NewCount,
    int UpdatedCount,
    int ConflictCount,
    IReadOnlyDictionary<string, int> CategoryCounts,
    int DroppedCount,
    IReadOnlyDictionary<string, int> DropReasons,
    double CoverageRatio,
    IReadOnlyList<string> Warnings,
    string HealthStatus)
{
    public static GameCoverageResult FromCounts(
        string gameCode,
        int candidateCount,
        int parsedCount,
        int droppedCount = 0,
        int newCount = 0,
        int updatedCount = 0,
        int conflictCount = 0,
        IReadOnlyDictionary<string, int>? categoryCounts = null,
        IReadOnlyDictionary<string, int>? dropReasons = null)
    {
        var ratio = candidateCount <= 0 ? (parsedCount == 0 ? 1d : 0d) : Math.Clamp((double)parsedCount / candidateCount, 0d, 1d);
        var warnings = new List<string>();
        if (candidateCount <= 0 && parsedCount > 0)
            warnings.Add($"{gameCode} 来源候选数无效，解析结果不能证明覆盖率；请检查来源健康。");
        var normalizedDropReasons = dropReasons is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(dropReasons, StringComparer.OrdinalIgnoreCase);
        if (droppedCount > 0 || ratio < 0.5d)
        {
            var reasonText = normalizedDropReasons.Count == 0 ? string.Empty : $"（{string.Join("、", normalizedDropReasons.Select(x => $"{DisplayDropReason(x.Key)}:{x.Value}"))}）";
            warnings.Add($"{gameCode} 解析覆盖率 {ratio:P0}，丢弃 {droppedCount} 条{reasonText}，可能漏采；请检查来源健康。");
        }
        return new(gameCode, candidateCount, parsedCount, newCount, updatedCount, conflictCount,
            categoryCounts ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), droppedCount, normalizedDropReasons, ratio, warnings,
            warnings.Count == 0 ? "HEALTHY" : "WARNING");
    }

    public static GameCoverageResult Failed(string gameCode, string error)
        => new(gameCode, 0, 0, 0, 0, 0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), 0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), 0d,
            [$"{gameCode} 刷新失败：{error}"], "FAILED");

    private static string DisplayDropReason(string reason) => reason switch
    {
        "ignored_rule" => "规则过滤",
        "unsupported_category" => "非目标类别",
        "missing_title" => "缺少标题",
        "missing_activity_end" => "缺少活动结束时间",
        "missing_source_time" => "缺少来源时间",
        "invalid_url" => "来源地址无效",
        "unsupported_channel" => "非目标频道",
        "duplicate_url_or_limit" => "重复或超出条数",
        "duplicate_identity" => "重复身份",
        "invalid_candidate" => "候选字段无效",
        "provider_filtered" => "来源过滤",
        _ => "其他原因"
    };
}

public sealed record GameRefreshJob(string GameCode, string DisplayName, Func<CancellationToken, Task<GameCoverageResult>> Run);
public sealed record GameRefreshProgress(string GameCode, string DisplayName, string Message, bool IsCompleted);
public sealed record GameRefreshReport(IReadOnlyList<GameCoverageResult> Games)
{
    public int TotalImported => Games.Sum(x => x.NewCount + x.UpdatedCount);
    public bool HasWarnings => Games.Any(x => x.HealthStatus != "HEALTHY");
}

public sealed class GameRefreshOrchestrator(IEnumerable<GameRefreshJob> jobs)
{
    private readonly IReadOnlyList<GameRefreshJob> _jobs = jobs.ToArray();

    public static GameRefreshOrchestrator CreateDefault(QimiaoDailyDbContext database, HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(client);
        return new([
            new GameRefreshJob("GENSHIN", "原神", ct => RefreshGenshinAsync(database, client, ct)),
            new GameRefreshJob("STARRAIL", "星铁", ct => RefreshStarRailAsync(database, client, ct)),
            new GameRefreshJob("NTE", "异环", ct => RefreshNteAsync(database, client, ct))
        ]);
    }

    public async Task<GameRefreshReport> RefreshAllAsync(IProgress<GameRefreshProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var results = new List<GameCoverageResult>(_jobs.Count);
        foreach (var job in _jobs)
        {
            progress?.Report(new GameRefreshProgress(job.GameCode, job.DisplayName, $"正在刷新{job.DisplayName}…", false));
            try
            {
                var result = await job.Run(cancellationToken);
                results.Add(result);
                progress?.Report(new GameRefreshProgress(job.GameCode, job.DisplayName,
                    $"{job.DisplayName} 发现 {result.ParsedCount} 条，新增 {result.NewCount}，更新 {result.UpdatedCount}，冲突 {result.ConflictCount}", true));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failed = GameCoverageResult.Failed(job.GameCode, ex.Message);
                results.Add(failed);
                progress?.Report(new GameRefreshProgress(job.GameCode, job.DisplayName, failed.Warnings[0], true));
            }
        }
        return new GameRefreshReport(results);
    }

    private static async Task<GameCoverageResult> RefreshGenshinAsync(QimiaoDailyDbContext database, HttpClient client, CancellationToken cancellationToken)
    {
        var candidates = await new GenshinAnnouncementProvider(client).CollectAsync(cancellationToken);
        return await ImportAsync(database, "GENSHIN", candidates, "genshin-announcement-v2", cancellationToken);
    }

    private static async Task<GameCoverageResult> RefreshStarRailAsync(QimiaoDailyDbContext database, HttpClient client, CancellationToken cancellationToken)
    {
        var candidates = await new StarRailAnnouncementProvider(client).CollectAsync(cancellationToken);
        return await ImportAsync(database, "STARRAIL", candidates, "starrail-announcement-v2", cancellationToken);
    }

    private static async Task<GameCoverageResult> RefreshNteAsync(QimiaoDailyDbContext database, HttpClient client, CancellationToken cancellationToken)
    {
        var provider = new NteOfficialWebsiteProvider(client);
        var events = await provider.CollectAsync(cancellationToken: cancellationToken);
        var videos = await provider.CollectVideosAsync(cancellationToken: cancellationToken);
        return await ImportAsync(database, "NTE", events.Candidates.Concat(videos), "nte-official-web-v2", cancellationToken,
            events.SourceCandidateCount + videos.Count, events.SourceRejectedCount, events.SourceRejectionReasons);
    }

    private static async Task<GameCoverageResult> ImportAsync(QimiaoDailyDbContext database, string gameCode, IEnumerable<GameCandidate> candidates, string parserVersion, CancellationToken cancellationToken,
        int? sourceCandidateCount = null, int sourceRejectedCount = 0, IReadOnlyDictionary<string, int>? sourceRejectionReasons = null)
    {
        var rawCandidates = candidates.ToArray();
        var list = rawCandidates.GroupBy(TimelineChangeClassifier.Identity, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
        var importer = new TimelineCandidateImportService(database);
        var newCount = 0;
        var updatedCount = 0;
        var conflictCount = 0;
        foreach (var candidate in list)
        {
            var result = await importer.ApplyCandidateAsync(candidate, parserVersion, "game-refresh", "orchestrated game refresh", cancellationToken);
            switch (result.ChangeKind)
            {
                case TimelineChangeKind.New: newCount++; break;
                case TimelineChangeKind.Conflict: conflictCount++; updatedCount++; break;
                case TimelineChangeKind.None: break;
                default: updatedCount++; break;
            }
        }

        var parsedCount = list.Count(x => !string.IsNullOrWhiteSpace(x.ExternalId) && !string.IsNullOrWhiteSpace(x.Title) &&
            !string.IsNullOrWhiteSpace(x.ItemType) && x.Evidence.Count > 0);
        var sourceCount = sourceCandidateCount ?? list.Select(x => x.SourceCandidateCount).Where(x => x > 0).DefaultIfEmpty(list.Length).Max();
        var providerRejected = Math.Max(sourceRejectedCount, list.Select(x => x.SourceRejectedCount).DefaultIfEmpty(0).Max());
        var droppedCount = Math.Max(0, sourceCount - parsedCount);
        var dropReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (sourceRejectionReasons is not null)
            foreach (var reason in sourceRejectionReasons)
                dropReasons[reason.Key] = reason.Value;
        foreach (var candidate in list)
            foreach (var reason in candidate.SourceRejectionReasons)
                dropReasons[reason.Key] = Math.Max(dropReasons.GetValueOrDefault(reason.Key), reason.Value);
        if (providerRejected > 0 && dropReasons.Count == 0) dropReasons["provider_filtered"] = providerRejected;
        var duplicateCount = Math.Max(0, rawCandidates.Length - list.Length);
        if (duplicateCount > 0) dropReasons["duplicate_identity"] = duplicateCount;
        var invalidCount = list.Count(x => string.IsNullOrWhiteSpace(x.ExternalId) || string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.ItemType) || x.Evidence.Count == 0);
        if (invalidCount > 0) dropReasons["invalid_candidate"] = invalidCount;
        var categories = list.GroupBy(x => x.ItemType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        return GameCoverageResult.FromCounts(gameCode, sourceCount, parsedCount, droppedCount, newCount, updatedCount, conflictCount, categories, dropReasons);
    }
}
