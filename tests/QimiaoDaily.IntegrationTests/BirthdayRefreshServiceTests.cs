using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;
using System.Net;

namespace QimiaoDaily.IntegrationTests;

public sealed class BirthdayRefreshServiceTests
{
    [Fact]
    public void BirthdaySourceCatalog_UsesOfficialCharacterEntryDefaultsWhenConfigIsMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qimiao-birthday-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sources = BirthdaySourceCatalog.Load(directory);
            Assert.Equal(51, sources.Count);
            Assert.Equal(1, sources[0].EntryPageId);
            Assert.Equal(51, sources[^1].EntryPageId);
            Assert.All(sources, source => Assert.Equal("GENSHIN", source.Franchise));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_PersistsOfficialBirthdayEnabledByDefault()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Kamisato Ayaka\",\"modules\":[{\"components\":[{\"data\":\"{\\\"key\\\":\\\"Birthday\\\",\\\"value\\\":[\\\"9/28\\\"]}\"}]}]}}}";
        using var client = new HttpClient(new Handler(body));
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
        Assert.True(await service.RefreshAsync(32));
        var item = await database.Birthdays.SingleAsync();
        Assert.Equal(VerificationStatus.VerifiedOfficial, item.VerificationStatus); Assert.True(item.Enabled); Assert.Equal(9, item.Month); Assert.Equal(28, item.Day);
    }

    [Fact]
    public async Task RefreshManyAsync_PersistsEachConfiguredOfficialSourceEnabledByDefault()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Kamisato Ayaka\",\"modules\":[{\"components\":[{\"data\":\"{\\\"key\\\":\\\"Birthday\\\",\\\"value\\\":[\\\"9/28\\\"]}\"}]}]}}}";
        using var client = new HttpClient(new Handler(body));
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
        Assert.Equal(2, await service.RefreshManyAsync([new BirthdaySourceRequest(32, "GENSHIN"), new BirthdaySourceRequest(33, "STARRAIL")]));
        Assert.Equal(2, await database.Birthdays.CountAsync());
        Assert.All(await database.Birthdays.ToListAsync(), x => Assert.True(x.Enabled));
    }

    [Fact]
    public async Task RefreshManyAsync_RejectsInvalidConfiguredSource()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient(new Handler("{}"));
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RefreshManyAsync([new BirthdaySourceRequest(0, "GENSHIN")]));
    }

    [Fact]
    public async Task RefreshAsync_PersistsUnknownBirthdayAsUnverifiedAndDisabled()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Unknown Character\",\"modules\":[]}}}";
        using var client = new HttpClient(new Handler(body));
        Assert.True(await new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client)).RefreshAsync(99));
        var item = await database.Birthdays.SingleAsync();
        Assert.Equal((0, 0), (item.Month, item.Day));
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.False(item.Enabled);
    }

    [Fact]
    public async Task RefreshManyAsync_PreservesUnknownForNteAndHi3Franchises()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Official Character Page\",\"modules\":[]}}}";
        using var client = new HttpClient(new Handler(body));
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));

        Assert.Equal(2, await service.RefreshManyAsync([new BirthdaySourceRequest(201, "NTE"), new BirthdaySourceRequest(202, "HI3")]));
        var records = await database.Birthdays.OrderBy(x => x.Franchise).ToListAsync();
        Assert.Equal(["HI3", "NTE"], records.Select(x => x.Franchise).ToArray());
        Assert.All(records, item =>
        {
            Assert.Equal((0, 0), (item.Month, item.Day));
            Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
            Assert.False(item.Enabled);
            Assert.Contains("UNKNOWN", item.Evidence);
        });
    }

    [Fact]
    public async Task RefreshCandidatesAsync_PersistsOfficialHi3CharacterListAsUnknownAndDisabled()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var candidates = new[]
        {
            new OfficialBirthdayCandidate("Kiana Kaslana", "HI3", 0, 0, "Honkai3OfficialCharacterList", "https://sg-public-api-static.hoyoverse.com/content_v2_user/app/5fcd2aa439ca4aea/getContentList", "Official character list API entry; birthday field unavailable; UNKNOWN", DateTimeOffset.UtcNow, true)
        };
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));

        Assert.Equal(1, await service.RefreshCandidatesAsync(candidates));
        var item = await database.Birthdays.SingleAsync();
        Assert.Equal("HI3", item.Franchise);
        Assert.Equal((0, 0), (item.Month, item.Day));
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.False(item.Enabled);
    }

    [Fact]
    public async Task RefreshCandidatesAsync_MergesLegacyNteRosterAliasIntoChinesePlaceholder()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.Add(new BirthdayEntity
        {
            Character = "yi", CanonicalCharacterNameZhCn = "yi", Franchise = "NTE", Month = 0, Day = 0,
            Source = "NteOfficialRoster", SourceUrl = NteOfficialRosterProvider.MainPageUrl,
            Evidence = "Official NTE roster slot: yi; birthday field unavailable; UNKNOWN.",
            VerificationStatus = VerificationStatus.Unverified, VerifiedAt = DateTimeOffset.UtcNow, Enabled = false
        });
        await database.SaveChangesAsync();
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));
        var candidate = new OfficialBirthdayCandidate(
            "官方角色槽位 01", "NTE", 0, 0, "NteOfficialRoster", NteOfficialRosterProvider.MainPageUrl,
            "Official NTE roster slot: yi; birthday field unavailable; UNKNOWN.", DateTimeOffset.UtcNow, true);

        await service.RefreshCandidatesAsync([candidate]);

        var item = Assert.Single(await database.Birthdays.ToListAsync());
        Assert.Equal("官方角色槽位 01", item.Character);
        Assert.Contains("yi", item.Aliases);
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.False(item.Enabled);
    }

    [Fact]
    public async Task GetCoverageAsync_DistinguishesVerifiedDatesFromUnknownRows()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        database.Birthdays.AddRange(
            new BirthdayEntity { Character = "Verified", Franchise = "GENSHIN", Month = 3, Day = 3, VerificationStatus = VerificationStatus.VerifiedOfficial, Source = "official", SourceUrl = "https://example.invalid/verified", Evidence = "Birthday: 3/3", VerifiedAt = DateTimeOffset.UtcNow, Enabled = false },
            new BirthdayEntity { Character = "Unknown", Franchise = "GENSHIN", Month = 0, Day = 0, VerificationStatus = VerificationStatus.Unverified, Source = "official", SourceUrl = "https://example.invalid/unknown", Evidence = "Birthday field unavailable; UNKNOWN", VerifiedAt = DateTimeOffset.UtcNow, Enabled = false });
        await database.SaveChangesAsync();

        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));
        var coverage = await service.GetCoverageAsync("GENSHIN");

        Assert.Equal(new BirthdayCoverageSnapshot("GENSHIN", 2, 1, 1, 0), coverage);
    }

    [Fact]
    public async Task GetCoverageReport_CountsKnownUnverifiedDatesAsKnownAndPending()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.AddRange(
            new BirthdayEntity { Character = "KnownPending", Franchise = "HI3", Month = 12, Day = 7, VerificationStatus = VerificationStatus.Unverified, Source = "third-party", SourceUrl = "https://example.invalid/a", Evidence = "one source", VerifiedAt = DateTimeOffset.UtcNow },
            new BirthdayEntity { Character = "Unknown", Franchise = "HI3", Month = 0, Day = 0, VerificationStatus = VerificationStatus.Unverified, Source = "third-party", SourceUrl = "https://example.invalid/b", Evidence = "UNKNOWN", VerifiedAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync();

        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));
        var result = Assert.Single(await service.GetCoverageReportAsync(["HI3"]));

        Assert.Equal((2, 1, 1, 1), (result.Total, result.Known, result.Unknown, result.Pending));
    }

    [Fact]
    public async Task RefreshManyResilientAsync_PreservesSuccessfulSourcesWhenOneFails()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Kamisato Ayaka\",\"modules\":[{\"components\":[{\"data\":\"{\\\"key\\\":\\\"Birthday\\\",\\\"value\\\":[\\\"9/28\\\"]}\"}]}]}}}";
        using var client = new HttpClient(new SelectiveHandler(body));
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(client));

        var result = await service.RefreshManyResilientAsync([new BirthdaySourceRequest(1, "GENSHIN"), new BirthdaySourceRequest(32, "GENSHIN")]);

        Assert.Equal(2, result.Attempted);
        Assert.Equal(1, result.Changed);
        Assert.Equal(1, result.VerifiedDates);
        Assert.Equal(1, result.Failed);
        Assert.Single(await database.Birthdays.ToListAsync());
        Assert.Contains("GENSHIN:1", result.Failures[0]);
    }

    [Fact]
    public async Task BirthdayReviewService_EnablesOnlyEvidenceBackedOfficialDate()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var item = new BirthdayEntity { Character = "Verified", Franchise = "GENSHIN", Month = 2, Day = 29, VerificationStatus = VerificationStatus.VerifiedOfficial, Source = "HoYoWikiOfficial", SourceUrl = "https://example.invalid/birthday", Evidence = "Birthday: 2/29", VerifiedAt = DateTimeOffset.UtcNow, Enabled = false };
        database.Birthdays.Add(item); await database.SaveChangesAsync();

        var service = new BirthdayReviewService(database);
        var candidates = await service.ListAsync();
        Assert.Single(candidates);
        Assert.True(candidates[0].CanEnable);
        await service.SetEnabledAsync(item.Id, true);

        Assert.True((await database.Birthdays.SingleAsync()).Enabled);
        await service.SetEnabledAsync(item.Id, false);
        Assert.False((await database.Birthdays.SingleAsync()).Enabled);
    }

    [Fact]
    public async Task BirthdayReviewService_RejectsUnknownOrInvalidDates()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var unknown = new BirthdayEntity { Character = "Unknown", Franchise = "HI3", Month = 0, Day = 0, VerificationStatus = VerificationStatus.Unverified, Source = "official", SourceUrl = "https://example.invalid/unknown", Evidence = "Birthday field unavailable; UNKNOWN", VerifiedAt = DateTimeOffset.UtcNow };
        var invalid = new BirthdayEntity { Character = "Invalid", Franchise = "GENSHIN", Month = 2, Day = 31, VerificationStatus = VerificationStatus.VerifiedOfficial, Source = "official", SourceUrl = "https://example.invalid/invalid", Evidence = "Birthday: 2/31", VerifiedAt = DateTimeOffset.UtcNow };
        database.Birthdays.AddRange(unknown, invalid); await database.SaveChangesAsync();
        var service = new BirthdayReviewService(database);

        Assert.All(await service.ListAsync(), x => Assert.False(x.CanEnable));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetEnabledAsync(unknown.Id, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetEnabledAsync(invalid.Id, true));
        Assert.All(await database.Birthdays.ToListAsync(), x => Assert.False(x.Enabled));
    }

    [Fact]
    public async Task BirthdayReviewService_SavesManualBirthdayEnabledByDefault()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new BirthdayReviewService(database);

        var id = await service.SaveManualAsync(null, "Manual Character", "NTE", 5, 6, "Official notice", "https://example.invalid/birthday", "Official evidence excerpt", VerificationStatus.VerifiedOfficial);
        var item = await database.Birthdays.SingleAsync();
        Assert.Equal(id, item.Id);
        Assert.True(item.Enabled);
        Assert.True(BirthdayReviewService.CanEnable(item));

        await service.SetEnabledAsync(id, false);
        Assert.False((await database.Birthdays.SingleAsync()).Enabled);
        await service.SaveManualAsync(id, "Manual Character", "NTE", 5, 6, "Official notice", "https://example.invalid/birthday", "Unverified excerpt", VerificationStatus.Unverified);
        item = await database.Birthdays.SingleAsync();
        Assert.False(item.Enabled);
        Assert.True(BirthdayReviewService.CanEnable(item));
    }

    [Fact]
    public async Task ThirdPartySingleSource_RetainsKnownDateAndEnablesItByDefault()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));

        await service.RefreshMergedCandidatesAsync([
            new MergedBirthdayCandidate("Hotori", "NTE", 12, 20,
                [new BirthdaySource("Hotori", "NTE", 12, 20, "NTE Fandom", "https://example.invalid/hotori")],
                "single third-party date", VerificationStatus.Unverified)
        ]);

        var item = await database.Birthdays.SingleAsync();
        Assert.Equal((12, 20), (item.Month, item.Day));
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.True(item.Enabled);
    }

    [Fact]
    public async Task RefreshMergedCandidates_MatchesExistingEnglishRosterBySourceAlias()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.Add(new BirthdayEntity { Character = "Kiana Kaslana", CanonicalCharacterNameZhCn = "Kiana Kaslana", Franchise = "HI3", Month = 0, Day = 0, Source = "official roster", SourceUrl = "https://example.invalid/roster", Evidence = "UNKNOWN", VerificationStatus = VerificationStatus.Unverified, VerifiedAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync();
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));

        await service.RefreshMergedCandidatesAsync([
            new MergedBirthdayCandidate("琪亚娜·卡斯兰娜", "HI3", 12, 7,
                [new BirthdaySource("Kiana Kaslana", "HI3", 12, 7, "Biligame HI3 Wiki", "https://example.invalid/biligame", "琪亚娜·卡斯兰娜"), new BirthdaySource("琪亚娜·卡斯兰娜", "HI3", 12, 7, "Baidu Baike", "https://example.invalid/baidu", "琪亚娜·卡斯兰娜")],
                "two sources agree", VerificationStatus.VerifiedMultiSource)
        ]);

        var rows = await database.Birthdays.ToListAsync();
        var item = Assert.Single(rows);
        Assert.Equal("琪亚娜·卡斯兰娜", item.Character);
        Assert.Equal((12, 7), (item.Month, item.Day));
        Assert.Equal(VerificationStatus.VerifiedMultiSource, item.VerificationStatus);
        Assert.Contains("Kiana Kaslana", item.Aliases);
    }

    [Fact]
    public async Task RefreshMergedCandidates_PreservesEnabledKnownDateWhenVerificationDowngrades()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.Add(new BirthdayEntity
        {
            Character = "Hotori", CanonicalCharacterNameZhCn = "Hotori", Franchise = "NTE", Month = 12, Day = 20,
            Source = "old", SourceUrl = "https://example.invalid/old", Evidence = "old", VerificationStatus = VerificationStatus.VerifiedMultiSource,
            VerifiedAt = DateTimeOffset.UtcNow, Enabled = true
        });
        await database.SaveChangesAsync();
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));

        await service.RefreshMergedCandidatesAsync([
            new MergedBirthdayCandidate("Hotori", "NTE", 12, 20,
                [new BirthdaySource("Hotori", "NTE", 12, 20, "NTE Fandom", "https://example.invalid/hotori")],
                "single third-party date", VerificationStatus.Unverified)
        ]);

        var item = await database.Birthdays.SingleAsync();
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.True(item.Enabled);
    }

    [Fact]
    public async Task RefreshMergedCandidates_NormalizesEnglishAndChineseRowsIntoOneCanonicalRecord()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.AddRange(
            new BirthdayEntity { Character = "Kiana Kaslana", CanonicalCharacterNameZhCn = "Kiana Kaslana", Franchise = "HI3", Month = 0, Day = 0, Source = "official", SourceUrl = "https://example.invalid/official", Evidence = "UNKNOWN", VerificationStatus = VerificationStatus.Unverified, VerifiedAt = DateTimeOffset.UtcNow },
            new BirthdayEntity { Character = "琪亚娜·卡斯兰娜", CanonicalCharacterNameZhCn = "琪亚娜·卡斯兰娜", Franchise = "HI3", Month = 12, Day = 7, Source = "wiki", SourceUrl = "https://example.invalid/wiki", Evidence = "Birthday: 12/7", VerificationStatus = VerificationStatus.VerifiedMultiSource, VerifiedAt = DateTimeOffset.UtcNow }
        );
        await database.SaveChangesAsync();
        var service = new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))));

        await service.NormalizeCanonicalNamesAsync(["HI3"]);

        var item = await database.Birthdays.SingleAsync();
        Assert.Equal("琪亚娜·卡斯兰娜", item.Character);
        Assert.Contains("Kiana Kaslana", item.Aliases);
        Assert.Equal((12, 7), (item.Month, item.Day));
        Assert.Equal(VerificationStatus.VerifiedMultiSource, item.VerificationStatus);
    }

    [Fact]
    public async Task BirthdayReviewService_AcceptsManualBirthdayWithoutEvidenceVerification()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new BirthdayReviewService(database);

        await service.SaveManualAsync(null, "Character", "GENSHIN", 1, 2, "MANUAL", string.Empty, string.Empty, VerificationStatus.Unverified);
        var item = await database.Birthdays.SingleAsync();
        Assert.Equal("Character", item.Character);
        Assert.True(BirthdayReviewService.CanEnable(item));
    }

    private sealed class Handler(string response) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) }); }
    private sealed class SelectiveHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(request.RequestUri?.Query.Contains("entry_page_id=1", StringComparison.Ordinal) == true
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
    }
}
