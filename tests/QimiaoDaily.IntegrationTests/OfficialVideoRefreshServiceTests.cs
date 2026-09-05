using System.Net;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class OfficialVideoRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_ContinuesAfterOneChannelFailureAndPersistsHealth()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient(new FeedHandler());

        var imported = await new OfficialVideoRefreshService(database, client).RefreshAsync();

        Assert.Equal(1, imported);
        Assert.Equal(1, await database.TimelineItems.CountAsync());
        var health = await database.ProviderHealthRecords.AsNoTracking().ToListAsync();
        Assert.Equal("FAILED", health.Single(x => x.ProviderName == "OfficialYoutubeRSS:Genshin").Status);
        Assert.Equal("HEALTHY", health.Single(x => x.ProviderName == "OfficialYoutubeRSS:StarRail").Status);
    }

    [Fact]
    public async Task RefreshAsync_AllChannelsFail_ThrowsAndPersistsBothFailures()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient(new AlwaysFailHandler());

        await Assert.ThrowsAsync<AggregateException>(() => new OfficialVideoRefreshService(database, client).RefreshAsync());

        var health = await database.ProviderHealthRecords.AsNoTracking().ToListAsync();
        Assert.Equal(2, health.Count);
        Assert.All(health, item => Assert.Equal("FAILED", item.Status));
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Query.Contains("UC2PeMPA8PAOp-bynLoCeMLA", StringComparison.Ordinal) == true)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <feed xmlns="http://www.w3.org/2005/Atom" xmlns:yt="http://www.youtube.com/xml/schemas/2015">
                      <title>Honkai: Star Rail</title>
                      <entry><yt:videoId>video-1</yt:videoId><title>Official version PV</title><published>2026-08-15T00:00:00Z</published></entry>
                    </feed>
                    """) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
