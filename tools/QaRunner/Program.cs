using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

var root = Environment.GetEnvironmentVariable("QIMIAO_QA_ROOT") ?? Path.Combine(Path.GetTempPath(), "QimiaoDaily-QA");
var paths = new QimiaoDailyPaths(root);
paths.EnsureDirectories();
var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={paths.DatabasePath}").Options;
await using var database = new QimiaoDailyDbContext(options);
QimiaoDatabaseInitializer.EnsureReady(database);
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
client.DefaultRequestHeaders.UserAgent.ParseAdd("QimiaoDaily-QA/1.0");
var operations = new OperationsService(database);

await RunGameRefreshAsync();
await Run("BGI", async () => { var service = new BgiRefreshService(database, new GitHubCommitProvider(client)); var now = DateTimeOffset.UtcNow; var count = 0; foreach (var repository in SourceSettings.Load(paths).BgiRepositories) count += await service.RefreshAsync(repository, now); return count; });
var qaCommit = (await database.GitCommitRecords.ToListAsync()).OrderByDescending(x => x.CommitterDate ?? x.AuthorDate).FirstOrDefault();
if (qaCommit is not null) { qaCommit.SelectedForReport = true; await database.SaveChangesAsync(); Console.WriteLine($"BGI selection: {qaCommit.Repository}/{qaCommit.Sha[..Math.Min(7, qaCommit.Sha.Length)]}"); }
await RunBirthdayCoverageAsync();
await Run("Honkai3OfficialCharacterList", async () =>
{
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
    var candidates = await new Honkai3OfficialCharacterProvider(client).CollectAsync(cancellationToken: default);
    var changed = await service.RefreshCandidatesAsync(candidates);
    await PrintBirthdayCoverageAsync("HI3");
    return changed;
});
var hi3Names = (await new Honkai3OfficialCharacterProvider(client).CollectAsync(cancellationToken: default)).Select(x => x.Character).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
IReadOnlyList<BirthdaySource> hi3BiligameSources = [];
IReadOnlyList<BirthdaySource> hi3BaiduSources = [];
IReadOnlyList<BirthdaySource> hi3MoegirlSources = [];
hi3BiligameSources = await RunBirthday("Hi3BiligameBirthday", () => new BiligameBirthdayProvider(client).CollectAsync(hi3Names));
hi3MoegirlSources = await RunBirthday("Hi3MoegirlBirthday", () => new MoegirlBirthdayProvider(client).CollectAsync(hi3Names));
hi3BaiduSources = await RunBirthday("Hi3BaiduBirthday", () => new BaiduBirthdayProvider(client).CollectAsync(hi3Names));
if (hi3BiligameSources.Count > 0 || hi3BaiduSources.Count > 0 || hi3MoegirlSources.Count > 0)
{
    var merged = hi3BiligameSources.Concat(hi3BaiduSources).Concat(hi3MoegirlSources)
        .GroupBy(ThirdPartyBirthdayProvider.ResolveCanonical, StringComparer.OrdinalIgnoreCase)
        .Select(ThirdPartyBirthdayProvider.Merge)
        .ToArray();
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
    await service.RefreshMergedCandidatesAsync(merged);
    await PrintBirthdayCoverageAsync("HI3", "HI3-after-public-sources");
}
var nteRosterProvider = new NteOfficialRosterProvider(client);
await Run("NteOfficialRoster", async () =>
{
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
    var candidates = await nteRosterProvider.CollectAsync();
    var changed = await service.RefreshCandidatesAsync(candidates);
    await PrintBirthdayCoverageAsync("NTE");
    return changed;
});
if (nteRosterProvider.UsedAuditedFallback)
    await operations.RecordFailureAsync("NteOfficialRoster", "WARNING", "Official roster fetch failed or was incomplete; used audited 16-slot fallback.", 0);
var nteGameSources = await RunBirthday("NteGameBirthday", async () =>
{
    var provider = new NteGameBirthdayProvider(client);
    return await provider.CollectAsync();
});
if (nteGameSources.Count > 0)
{
    var merged = nteGameSources.Select(source => new MergedBirthdayCandidate(
        source.Character, "NTE", source.Month, source.Day, [source],
        source.EvidenceExcerpt ?? "NTEGame single-source birthday candidate; pending second-source verification.",
        VerificationStatus.Unverified)).ToArray();
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
    await service.RefreshMergedCandidatesAsync(merged);
    await PrintBirthdayCoverageAsync("NTE", "NTE-after-ntegame");
}
await Run("NteFandomBirthday", async () =>
{
    var thirdParty = new ThirdPartyBirthdayProvider(client);
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client), thirdParty);
    var candidates = await thirdParty.CollectFandomCharactersAsync();
    var changed = await service.RefreshMergedCandidatesAsync(candidates);
    await PrintBirthdayCoverageAsync("NTE");
    return changed;
});
try
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Pixiv DPAPI QA requires Windows.");
    var cookie = new SecureSettingsStore(paths).TryGet("pixiv_session");
    var outcome = await new ArtworkDailyRefreshService(new PixivArtworkProvider(client, cookie), new ArtworkImportService(database)).RefreshAsync(5);
    if (outcome.Status == ArtworkFetchStatus.Healthy) await operations.RecordSuccessAsync("Pixiv", outcome.Imported, 0);
    else await operations.RecordFailureAsync("Pixiv", outcome.Status == ArtworkFetchStatus.LoginRequired ? "LOGIN_REQUIRED" : "BLOCKED", outcome.Message, 0);
    Console.WriteLine($"Pixiv: {outcome.Status}; imported={outcome.Imported}; {outcome.Message}");
}
catch (Exception ex)
{
    await operations.RecordFailureAsync("Pixiv", "FAILED", ex.Message, 0);
    Console.WriteLine($"Pixiv: FAILED; {ex.Message}");
}
try
{
    var result = await new NteBilibiliOfficialProvider(client).CollectAsync();
    if (result.Status == SourceFetchStatus.Healthy) await operations.RecordSuccessAsync("NteBilibiliOfficial", result.Candidates.Count, 0);
    else await operations.RecordFailureAsync("NteBilibiliOfficial", result.Status == SourceFetchStatus.Blocked ? "BLOCKED" : "FAILED", result.Message, 0);
    Console.WriteLine($"NteBilibiliOfficial: {result.Status}; {result.Message}");
}
catch (Exception ex)
{
    await operations.RecordFailureAsync("NteBilibiliOfficial", "FAILED", ex.Message, 0);
    Console.WriteLine($"NteBilibiliOfficial: FAILED; {ex.Message}");
}

Console.WriteLine($"TimelineItems={await database.TimelineItems.CountAsync()}; Evidence={await database.Evidence.CountAsync()}; EndgameRules={await database.EndgameCycleRules.CountAsync()}; EndgameInstances={await database.EndgameCycleInstances.CountAsync()}; GitCommits={await database.GitCommitRecords.CountAsync()}; Database={paths.DatabasePath}");
var reviewCandidate = await database.TimelineItems.FirstOrDefaultAsync();
if (reviewCandidate is not null)
{
    await new TimelineReviewService(database).ConfirmAsync(reviewCandidate.Id, "qa-user", "Real network QA confirmation", DateTimeOffset.UtcNow);
    var reportDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").Date);
    var report = new DailyReportService(database);
    await report.BuildAutomaticSectionsAsync(reportDate);
    Console.WriteLine($"ReviewGate: confirmed={reviewCandidate.Id}; reportChars={(await report.ComposeAsync(reportDate)).Length}");
}

async Task Run(string provider, Func<Task<int>> action)
{
    var started = DateTimeOffset.UtcNow;
    try
    {
        var count = await action();
        await operations.RecordSuccessAsync(provider, count, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);
        Console.WriteLine($"{provider}: HEALTHY; imported={count}");
    }
    catch (Exception ex)
    {
        await operations.RecordFailureAsync(provider, "FAILED", ex.Message, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);
        Console.WriteLine($"{provider}: FAILED; {ex.Message}");
    }
}

async Task<IReadOnlyList<BirthdaySource>> RunBirthday(string provider, Func<Task<IReadOnlyList<BirthdaySource>>> action)
{
    var started = DateTimeOffset.UtcNow;
    try
    {
        var sources = await action();
        var health = BirthdayProviderHealth.Summarize(sources);
        var elapsed = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
        if (health.Status == "FAILED")
            await operations.RecordFailureAsync(provider, health.Status, health.Message, elapsed, itemCount: health.Known);
        else
            await operations.RecordSuccessAsync(provider, health.Known, elapsed, "BIRTHDAY_COVERAGE", status: health.Status);
        Console.WriteLine($"{provider}: {health.Status}; known={health.Known}; unknown={health.Unknown}; total={health.Total}");
        return sources;
    }
    catch (Exception ex)
    {
        await operations.RecordFailureAsync(provider, "FAILED", ex.Message, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);
        Console.WriteLine($"{provider}: FAILED; {ex.Message}");
        return [];
    }
}

async Task RunGameRefreshAsync()
{
    var report = await GameRefreshOrchestrator.CreateDefault(database, client).RefreshAllAsync();
    foreach (var game in report.Games)
    {
        var reasons = game.DropReasons.Count == 0
            ? "none"
            : string.Join(",", game.DropReasons.Select(x => $"{x.Key}={x.Value}"));
        Console.WriteLine($"Coverage:{game.GameCode}; source={game.CandidateCount}; parsed={game.ParsedCount}; dropped={game.DroppedCount}; ratio={game.CoverageRatio:P0}; reasons={reasons}; health={game.HealthStatus}");
        var provider = game.GameCode switch
        {
            "GENSHIN" => "GenshinOfficial",
            "STARRAIL" => "StarRailOfficial",
            "NTE" => "NteOfficialWebsite",
            _ => game.GameCode
        };
        var elapsed = 0L;
        if (game.HealthStatus == "HEALTHY")
            await operations.RecordSuccessAsync(provider, game.ParsedCount, elapsed, "COVERAGE");
        else
            await operations.RecordFailureAsync(provider, game.HealthStatus, string.Join(" | ", game.Warnings), elapsed, itemCount: game.ParsedCount);
    }
    Console.WriteLine($"GameRefreshTotalImported={report.TotalImported}");
}

async Task RunBirthdayCoverageAsync()
{
    var started = DateTimeOffset.UtcNow;
    var sources = BirthdaySourceCatalog.Load(paths.ConfigDirectory);
    if (sources.Count == 0) sources = [new BirthdaySourceRequest(32, "GENSHIN")];

    var provider = new HoYoWikiBirthdayProvider(client);
    var service = new BirthdayRefreshService(database, provider);
    var result = await service.RefreshManyResilientAsync(sources);

    var status = result.Failed == 0 ? "HEALTHY" : "WARNING";
    var elapsed = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
    var message = result.Failed == 0 ? null : string.Join(" | ", result.Failures);
    if (result.Failed == 0)
        await operations.RecordSuccessAsync("BirthdayHoYoWiki", result.Attempted, elapsed);
    else
        await operations.RecordFailureAsync("BirthdayHoYoWiki", status, message!, elapsed);

    Console.WriteLine($"BirthdayHoYoWiki: {status}; attempted={result.Attempted}; known={result.VerifiedDates}; unknown={result.Unknown}; changed={result.Changed}; failures={result.Failed}");
    await PrintBirthdayCoverageAsync("GENSHIN");
    foreach (var failure in result.Failures) Console.WriteLine($"Birthday failure: {failure}");
}

async Task PrintBirthdayCoverageAsync(string franchise, string? label = null)
{
    var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
    var coverage = (await service.GetCoverageReportAsync([franchise])).Single();
    Console.WriteLine($"BirthdayCoverage:{label ?? coverage.Franchise}; total={coverage.Total}; known={coverage.Known}; unknown={coverage.Unknown}; pending={coverage.Pending}");
}
