using System.Net;
using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class NteBilibiliOfficialProviderTests
{
 [Fact] public async Task CollectAsync_ReportsBlocked_InsteadOfPretendingSuccess(){using var client=new HttpClient(new BlockedHandler());var result=await new NteBilibiliOfficialProvider(client).CollectAsync();Assert.Equal(SourceFetchStatus.Blocked,result.Status);Assert.Empty(result.Candidates);}
 [Fact] public async Task CollectAsync_NormalizesUtcPublishTimeToShanghai(){const long epoch=1786701600;var body=$"{{\"code\":0,\"data\":{{\"list\":{{\"vlist\":[{{\"bvid\":\"BV1\",\"title\":\"Official video\",\"created\":{epoch}}}]}}}}}}";var handler=new H(body);using var client=new HttpClient(handler);var result=await new NteBilibiliOfficialProvider(client).CollectAsync();var item=Assert.Single(result.Candidates);Assert.Equal("UTC",item.SourceTimezone);Assert.Equal(18,item.NormalizedTime!.Value.Hour);Assert.Equal("UTC",item.Evidence[0].OriginalTimezone);Assert.Contains("Mozilla", handler.UserAgent);Assert.Equal("https://space.bilibili.com/3546636978489848/", handler.Referrer);}
 private sealed class BlockedHandler:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":-799}")});}
 private sealed class H(string body):HttpMessageHandler{public string UserAgent { get; private set; } = string.Empty; public string Referrer { get; private set; } = string.Empty; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){UserAgent=r.Headers.UserAgent.ToString();Referrer=r.Headers.Referrer?.ToString()??string.Empty;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});}}
}
