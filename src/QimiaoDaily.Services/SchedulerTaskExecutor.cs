using System.Net;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// Central registry of scheduler handlers used by both the desktop shell and the background loop.
public sealed class SchedulerTaskExecutor(QimiaoDailyDbContext database, HttpClient client, QimiaoDailyPaths paths)
{
    public GameRefreshReport? LastGameRefreshReport { get; private set; }
    public BirthdayRefreshReport? LastBirthdayRefreshReport { get; private set; }

    public async Task<int> ExecuteAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        if (!SchedulerScheduleCatalog.IsScheduledTask(taskKey))
            throw new InvalidOperationException($"Scheduler task '{taskKey}' is retired and cannot be executed automatically.");

        return taskKey switch
        {
            "video_refresh" => await ImportVideosAsync(cancellationToken),
            "preview_refresh" => await ImportVideosAsync(cancellationToken),
            "github_bgi_refresh" => await RefreshGitHubAsync(SourceSettings.Load(paths).BgiRepositories, cancellationToken),
            "github_scripts_refresh" => await RefreshGitHubAsync(SourceSettings.Load(paths).BgiRepositories.Skip(1).Take(1), cancellationToken),
            "nte_bilibili_refresh" => await RefreshNteBilibiliAsync(cancellationToken),
            "artwork_daily_search" => await RefreshArtworkAsync(cancellationToken),
            "calendar_refresh" => await RefreshCalendarAsync(cancellationToken),
            "archive_cleanup" => await new TimelineArchiveService(database).ArchiveExpiredAsync(DateTimeOffset.UtcNow, cancellationToken: cancellationToken),
            "report_build" => await BuildReportAsync(cancellationToken),
            _ => throw new InvalidOperationException("Task handler is not registered for " + taskKey + ".")
        };
    }

    private async Task<int> ImportVideosAsync(CancellationToken cancellationToken)
    {
        return await new OfficialVideoRefreshService(database, client).RefreshAsync(cancellationToken);
    }

    private async Task<int> RefreshGameDataAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var report = await GameRefreshOrchestrator.CreateDefault(database, client).RefreshAllAsync(cancellationToken: cancellationToken);
        LastGameRefreshReport = report;
        var operations = new OperationsService(database);
        foreach (var game in report.Games)
        {
            var providerName = game.GameCode switch
            {
                "GENSHIN" => "GenshinOfficial",
                "STARRAIL" => "StarRailOfficial",
                "NTE" => "NteOfficialWebsite",
                _ => game.GameCode
            };
            if (game.HealthStatus == "HEALTHY")
                await operations.RecordSuccessAsync(providerName, game.ParsedCount, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, "COVERAGE", cancellationToken);
            else
                await operations.RecordFailureAsync(providerName, game.HealthStatus, string.Join(" | ", game.Warnings), (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken, game.ParsedCount);
        }

        if (report.Games.Count > 0 && report.Games.All(x => x.HealthStatus == "FAILED"))
            throw new AggregateException("All game refreshes failed.", report.Games.SelectMany(x => x.Warnings.Select(error => new InvalidOperationException(error))));
        return report.TotalImported;
    }

    private async Task<int> RefreshEndgameAsync(CancellationToken cancellationToken)
    {
        return await RunIndependentAsync(
        [
            new("GenshinOfficial", () => new GenshinRefreshService(database, new GenshinAnnouncementProvider(client)).RefreshAsync(cancellationToken, "ENDGAME")),
            new("StarRailOfficial", () => new StarRailRefreshService(database, new StarRailAnnouncementProvider(client)).RefreshAsync(cancellationToken, "ENDGAME"))
        ], cancellationToken);
    }

    private async Task<int> RefreshGitHubAsync(IEnumerable<string> repositories, CancellationToken cancellationToken)
    {
        var selected = repositories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("No BGI repositories are configured.");
        return await RunWithHealthAsync("BGI GitHub", async () =>
        {
            var service = new BgiRefreshService(database, new GitHubCommitProvider(client));
            var count = 0;
            foreach (var repository in selected)
                count += await service.RefreshAsync(repository, DateTimeOffset.UtcNow, cancellationToken);
            return count;
        }, cancellationToken);
    }

    private async Task<int> RefreshArtworkAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Pixiv Session storage requires Windows.");
        var cookie = new SecureSettingsStore(paths).TryGet("pixiv_session");
        var started = DateTimeOffset.UtcNow;
        var settings = SourceSettings.Load(paths);
        var outcome = await new ArtworkDailyRefreshService(new PixivArtworkProvider(client, cookie), new ArtworkImportService(database), database)
            .RefreshAsync(
                target: settings.ArtworkTargetCount,
                directArtworkIds: settings.ArtworkIds,
                characterSearches: ArtworkCharacterCatalog.GetDailySelection(settings.ArtworkTargetCount),
                resultsPerCharacter: 3,
                cancellationToken: cancellationToken);
        var operations = new OperationsService(database);
        if (outcome.Status != ArtworkFetchStatus.Healthy)
        {
            var status = outcome.Status switch
            {
                ArtworkFetchStatus.LoginRequired => "LOGIN_REQUIRED",
                ArtworkFetchStatus.Blocked => "BLOCKED",
                _ => "FAILED"
            };
            await operations.RecordFailureAsync("Pixiv", status, outcome.Message, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken, outcome.Imported);
            throw new InvalidOperationException(outcome.Message);
        }

        await operations.RecordSuccessAsync("Pixiv", outcome.Imported, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            parserStatus: outcome.Imported >= outcome.Target ? "OK" : "PARTIAL",
            cancellationToken: cancellationToken,
            status: outcome.Imported >= outcome.Target ? "HEALTHY" : "PARTIAL");
        return outcome.Imported;
    }

    private async Task<int> RefreshBirthdayAsync(CancellationToken cancellationToken)
    {
        var sources = BirthdaySourceCatalog.Load(paths.ConfigDirectory);
        if (sources.Count == 0) sources = [new BirthdaySourceRequest(32, "GENSHIN")];
        var thirdParty = new ThirdPartyBirthdayProvider(client);
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client), thirdParty);
        var started = DateTimeOffset.UtcNow;
        var genshinResult = await service.RefreshManyResilientAsync(sources, cancellationToken);
        var failedSources = genshinResult.Failed;
        if (genshinResult.Failed == 0)
            await new OperationsService(database).RecordSuccessAsync("BirthdayHoYoWiki", genshinResult.Attempted,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken: cancellationToken);
        else
            await new OperationsService(database).RecordFailureAsync("BirthdayHoYoWiki", "WARNING", string.Join(" | ", genshinResult.Failures),
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken);
        var hi3 = 0;
        IReadOnlyList<string> hi3Roster = [];
        IReadOnlyList<BirthdaySource> hi3BiligameSources = [];
        IReadOnlyList<BirthdaySource> hi3BaiduSources = [];
        IReadOnlyList<BirthdaySource> hi3MoegirlSources = [];
        try
        {
            hi3 = await RunWithHealthAsync("Honkai3OfficialCharacterList", async () =>
            {
                var candidates = await new Honkai3OfficialCharacterProvider(client).CollectAsync(cancellationToken: cancellationToken);
                hi3Roster = candidates.Select(x => x.Character).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return await service.RefreshCandidatesAsync(candidates, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            failedSources++;
        }
        if (hi3Roster.Count > 0)
        {
            try
            {
                var result = await CollectBirthdaySourcesAsync("Hi3BiligameBirthday", () => new BiligameBirthdayProvider(client).CollectAsync(hi3Roster, cancellationToken), cancellationToken);
                hi3BiligameSources = result.Sources;
                if (result.Status != "HEALTHY") failedSources++;
            }
            catch { failedSources++; }
            try
            {
                var result = await CollectBirthdaySourcesAsync("Hi3MoegirlBirthday", () => new MoegirlBirthdayProvider(client).CollectAsync(hi3Roster, cancellationToken), cancellationToken);
                hi3MoegirlSources = result.Sources;
                if (result.Status != "HEALTHY") failedSources++;
            }
            catch { failedSources++; }
            try
            {
                var result = await CollectBirthdaySourcesAsync("Hi3BaiduBirthday", () => new BaiduBirthdayProvider(client).CollectAsync(hi3Roster, cancellationToken), cancellationToken);
                hi3BaiduSources = result.Sources;
                if (result.Status != "HEALTHY") failedSources++;
            }
            catch { failedSources++; }
            var mergedHi3 = hi3BiligameSources.Concat(hi3BaiduSources).Concat(hi3MoegirlSources)
                .GroupBy(ThirdPartyBirthdayProvider.ResolveCanonical, StringComparer.OrdinalIgnoreCase)
                .Select(ThirdPartyBirthdayProvider.Merge)
                .ToArray();
            if (mergedHi3.Length > 0) hi3 += await service.RefreshMergedCandidatesAsync(mergedHi3, cancellationToken);
        }
        var nte = 0;
        try
        {
            var rosterProvider = new NteOfficialRosterProvider(client);
            var rosterChanges = await RunWithHealthAsync("NteOfficialRoster", async () =>
            {
                var roster = await rosterProvider.CollectAsync(cancellationToken);
                return await service.RefreshCandidatesAsync(roster, cancellationToken);
            }, cancellationToken);
            if (rosterProvider.UsedAuditedFallback)
                await new OperationsService(database).RecordFailureAsync(
                    "NteOfficialRoster", "WARNING", "Official NTE roster fetch failed or was incomplete; using audited 16-slot fallback.",
                    0, cancellationToken);
            nte += rosterChanges;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Keep the NTE birthday refresh resilient when the official page is unavailable.
            failedSources++;
        }
        IReadOnlyList<BirthdaySource> nteGameBirthdaySources = [];
        try
        {
            await RunWithHealthAsync("NteGameBirthday", async () =>
            {
                nteGameBirthdaySources = await new NteGameBirthdayProvider(client).CollectAsync(cancellationToken);
                return nteGameBirthdaySources.Count;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A non-official enrichment outage must not hide the official roster.
            failedSources++;
        }
        IReadOnlyList<MergedBirthdayCandidate> nteFandomBirthdayCandidates = [];
        try
        {
            await RunWithHealthAsync("NteFandomBirthday", async () =>
            {
                nteFandomBirthdayCandidates = await thirdParty.CollectFandomCharactersAsync(cancellationToken);
                return nteFandomBirthdayCandidates.Count;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The NTE third-party source is isolated; the official/HI3 results remain usable.
            failedSources++;
        }
        IReadOnlyList<BirthdaySource> nteNevernessBirthdaySources = [];
        try
        {
            await RunWithHealthAsync("NteNevernessGgBirthday", async () =>
            {
                nteNevernessBirthdaySources = await new NteNevernessGgBirthdayProvider(client).CollectAsync(cancellationToken);
                return nteNevernessBirthdaySources.Count;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            failedSources++;
        }
        var nteSources = nteGameBirthdaySources
            .Concat(nteFandomBirthdayCandidates.SelectMany(candidate => candidate.Sources))
            .Concat(nteNevernessBirthdaySources)
            .ToArray();
        if (nteSources.Length > 0)
            nte += await service.RefreshMergedCandidatesAsync(
                ThirdPartyBirthdayProvider.MergeByCanonicalCharacter(nteSources), cancellationToken);
        var coverage = await service.GetCoverageReportAsync(["GENSHIN", "HI3", "NTE"], cancellationToken);
        LastBirthdayRefreshReport = new BirthdayRefreshReport(coverage, failedSources);
        return genshinResult.Changed + hi3 + nte;
    }

    private async Task<int> RefreshNteBilibiliAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var result = await new NteBilibiliOfficialProvider(client).CollectAsync(cancellationToken);
        if (result.Status != SourceFetchStatus.Healthy)
        {
            var status = result.Status == SourceFetchStatus.Blocked ? "BLOCKED" : "FAILED";
            await new OperationsService(database).RecordFailureAsync("NteBilibiliOfficial", status, result.Message,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken);
            throw new InvalidOperationException(result.Message);
        }

        var importer = new OfficialVideoImportService(database);
        var imported = 0;
        foreach (var candidate in result.Candidates)
            if (await importer.ImportAsync(candidate, cancellationToken)) imported++;
        await new OperationsService(database).RecordSuccessAsync("NteBilibiliOfficial", result.Candidates.Count,
            (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken: cancellationToken);
        return imported;
    }

    private async Task<int> RefreshCalendarAsync(CancellationToken cancellationToken)
    {
        var china = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time");
        var year = await new CalendarService(database).ForYearAsync(china.Year, cancellationToken);
        return year.Sum(x => x.Value.Count);
    }

    private async Task<int> BuildReportAsync(CancellationToken cancellationToken)
    {
        var china = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time");
        await new DailyReportService(database).BuildAutomaticSectionsAsync(DateOnly.FromDateTime(china.Date), cancellationToken);
        return 0;
    }

    private async Task<int> RunIndependentAsync(IReadOnlyList<ProviderWork> providers, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        var count = 0;
        foreach (var provider in providers)
        {
            try { count += await RunWithHealthAsync(provider.Name, provider.Action, cancellationToken); }
            catch (Exception ex) { failures.Add(new InvalidOperationException(provider.Name + ": " + ex.Message, ex)); }
        }

        if (failures.Count == providers.Count) throw new AggregateException("All providers failed.", failures);
        return count;
    }

    private async Task<int> RunWithHealthAsync(string providerName, Func<Task<int>> action, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var count = await action();
            await new OperationsService(database).RecordSuccessAsync(providerName, count,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken: cancellationToken);
            return count;
        }
        catch (Exception ex)
        {
            var status = ex is HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests }
                ? "BLOCKED"
                : "FAILED";
            await new OperationsService(database).RecordFailureAsync(providerName, status, ex.Message,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken);
            throw;
        }
    }

    private async Task<BirthdayProviderCollectionResult> CollectBirthdaySourcesAsync(
        string providerName,
        Func<Task<IReadOnlyList<BirthdaySource>>> action,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var sources = await action();
            var summary = BirthdayProviderHealth.Summarize(sources);
            var latency = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
            if (summary.Status == "FAILED")
                await new OperationsService(database).RecordFailureAsync(providerName, summary.Status, summary.Message, latency, cancellationToken, summary.Known);
            else
                await new OperationsService(database).RecordSuccessAsync(providerName, summary.Known, latency, "BIRTHDAY_COVERAGE", cancellationToken, summary.Status);
            return new(sources, summary.Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await new OperationsService(database).RecordFailureAsync(providerName, "FAILED", ex.Message,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, cancellationToken);
            throw;
        }
    }

    private sealed record ProviderWork(string Name, Func<Task<int>> Action);
}

public sealed record BirthdayRefreshReport(IReadOnlyList<BirthdayCoverageResult> Games, int FailedSourceCount);
public sealed record BirthdayProviderCollectionResult(IReadOnlyList<BirthdaySource> Sources, string Status);
