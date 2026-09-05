using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class BirthdayCoverageTests
{
    [Fact]
    public async Task CoverageReportIncludesGenshinHi3AndNteWithUnknownCounts()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.Birthdays.AddRange(
            new BirthdayEntity { Character = "托马", Franchise = "GENSHIN", Month = 1, Day = 9, VerificationStatus = VerificationStatus.VerifiedOfficial, Source = "official", SourceUrl = "https://example.invalid/g", Evidence = "1/9", VerifiedAt = DateTimeOffset.UtcNow },
            new BirthdayEntity { Character = "Kiana", Franchise = "HI3", Month = 0, Day = 0, VerificationStatus = VerificationStatus.Unverified, Source = "roster", SourceUrl = "https://example.invalid/h", Evidence = "UNKNOWN", VerifiedAt = DateTimeOffset.UtcNow },
            new BirthdayEntity { Character = "奈莉", Franchise = "NTE", Month = 4, Day = 2, VerificationStatus = VerificationStatus.VerifiedMultiSource, Source = "wiki", SourceUrl = "https://example.invalid/n", Evidence = "4/2", VerifiedAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync();

        var report = await new BirthdayRefreshService(database, new HoYoWikiBirthdayProvider(new HttpClient(new Handler("{}"))))
            .GetCoverageReportAsync(["GENSHIN", "HI3", "NTE"]);

        Assert.Equal(["GENSHIN", "HI3", "NTE"], report.Select(x => x.Franchise));
        Assert.Equal((1, 0), (report[0].Known, report[0].Unknown));
        Assert.Equal((0, 1), (report[1].Known, report[1].Unknown));
        Assert.Equal((1, 0), (report[2].Known, report[2].Unknown));
    }

    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
