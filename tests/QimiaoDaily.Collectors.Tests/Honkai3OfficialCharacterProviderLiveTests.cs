using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class Honkai3OfficialCharacterProviderLiveTests
{
    [Fact]
    public async Task CollectAsync_ReadsOfficialCharacterList_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var candidates = await new Honkai3OfficialCharacterProvider(client).CollectAsync();
        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, x => x.Character == "Kiana Kaslana");
        Assert.All(candidates, x => Assert.True(x.IsUnknown));
    }
}
