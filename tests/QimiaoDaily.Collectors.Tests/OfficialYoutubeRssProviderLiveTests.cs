using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class OfficialYoutubeRssProviderLiveTests
{
 [Fact] public async Task CollectAsync_ReadsRealGenshinAndStarRailOfficialFeeds_WhenExplicitlyEnabled(){if(Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS")!="1")return;using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var p=new OfficialYoutubeRssProvider(client);var g=await p.CollectAsync("GENSHIN",OfficialYoutubeRssProvider.GenshinChannelId,"Genshin Impact");var s=await p.CollectAsync("STARRAIL",OfficialYoutubeRssProvider.StarRailChannelId,"Honkai: Star Rail");Assert.NotEmpty(g);Assert.NotEmpty(s);Assert.All(g.Concat(s),x=>Assert.Single(x.Evidence));}
}
