using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class PixivArtworkProviderLiveTests
{
    [Fact]
    public async Task FetchAsync_ReadsRealPixivArtwork_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var result = await new PixivArtworkProvider(client).FetchAsync("100000000");
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status); Assert.NotNull(result.Candidate); Assert.Equal("PIXIV", result.Candidate!.Platform);
    }
    [Fact]
    public async Task FetchDailyRankingAsync_ReadsDirectPixivDailyRanking_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; var result = await new PixivArtworkProvider(client).FetchDailyRankingAsync();
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status); Assert.NotEmpty(result.Candidates); Assert.True(result.Candidates.Count <= 30);
    }
}
