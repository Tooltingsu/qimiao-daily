using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class StarRailAnnouncementProviderLiveTests
{
 [Fact] public async Task CollectAsync_UsesLiveOfficialEndpoint_WhenExplicitlyEnabled(){if(Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS")!="1")return;using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var rows=await new StarRailAnnouncementProvider(client).CollectAsync();Assert.NotEmpty(rows);Assert.All(rows,x=>Assert.Equal("STARRAIL",x.GameCode));Assert.All(rows,x=>Assert.Equal(2,x.Evidence.Count));}
}
