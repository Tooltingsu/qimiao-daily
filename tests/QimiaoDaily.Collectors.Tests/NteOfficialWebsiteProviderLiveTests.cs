using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class NteOfficialWebsiteProviderLiveTests
{
    [Fact]
    public async Task CollectAsync_ReadsOfficialWebsite_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var result = await new NteOfficialWebsiteProvider(client).CollectAsync(10);
        Assert.Equal(SourceFetchStatus.Healthy, result.Status);
        Assert.NotEmpty(result.Candidates);
        Assert.All(result.Candidates, x => Assert.StartsWith("https://nte.perfectworld.com/", x.Evidence.Single().SourceUrl));
    }
}
