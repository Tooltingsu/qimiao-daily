using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class HoYoWikiBirthdayProviderLiveTests
{
    [Fact]
    public async Task CollectAsync_ReadsAyakaBirthdayFromOfficialHoYoWiki_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var candidate = await new HoYoWikiBirthdayProvider(client).CollectAsync(32);
        Assert.Equal("Kamisato Ayaka", candidate.Character); Assert.Equal((9, 28), (candidate.Month, candidate.Day)); Assert.StartsWith(HoYoWikiBirthdayProvider.EntryPageUrl, candidate.SourceUrl);
    }

    [Fact]
    public async Task CollectAsync_ReadsAnotherOfficialBirthdayAndPreservesOfficialUnknown_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var provider = new HoYoWikiBirthdayProvider(client);
        var qiqi = await provider.CollectAsync(1);
        var traveler = await provider.CollectAsync(17);
        Assert.Equal("Qiqi", qiqi.Character);
        Assert.Equal((3, 3), (qiqi.Month, qiqi.Day));
        Assert.False(qiqi.IsUnknown);
        Assert.Equal("Traveler (Geo)", traveler.Character);
        Assert.Equal((0, 0), (traveler.Month, traveler.Day));
        Assert.True(traveler.IsUnknown);
    }
}
