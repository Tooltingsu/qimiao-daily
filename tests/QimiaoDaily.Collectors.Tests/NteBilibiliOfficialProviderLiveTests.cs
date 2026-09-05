using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class NteBilibiliOfficialProviderLiveTests
{
 [Fact] public async Task VerifyOfficialVideoAsync_ValidatesRealOfficialNteVideo_WhenExplicitlyEnabled(){if(Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS")!="1")return;using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var item=await new NteBilibiliOfficialProvider(client).VerifyOfficialVideoAsync("BV1W1uq6zEdG");Assert.Equal("NTE",item.GameCode);Assert.Equal("VIDEO",item.ItemType);Assert.Single(item.Evidence);Assert.NotEmpty(item.Title);}
}
