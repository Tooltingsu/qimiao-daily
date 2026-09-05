using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class GenshinAnnouncementProviderLiveTests
{
    [Fact]
    public async Task CollectAsync_UsesLiveOfficialAnnouncementEndpoints_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var candidates = await new GenshinAnnouncementProvider(client).CollectAsync();
        Assert.NotEmpty(candidates);
        Assert.All(candidates, x => Assert.Equal("GENSHIN", x.GameCode));
        Assert.All(candidates, x => Assert.Equal(2, x.Evidence.Count));
    }
}
