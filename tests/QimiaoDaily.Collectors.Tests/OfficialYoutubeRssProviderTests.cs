using System.Net;
using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class OfficialYoutubeRssProviderTests
{
 [Fact] public async Task CollectAsync_ParsesVerifiedChannelFeed(){const string rss="<feed xmlns='http://www.w3.org/2005/Atom' xmlns:yt='http://www.youtube.com/xml/schemas/2015'><title>Genshin Impact</title><entry><title>Version PV</title><published>2026-08-14T10:00:00Z</published><yt:videoId>abc</yt:videoId></entry></feed>";using var client=new HttpClient(new H(rss));var rows=await new OfficialYoutubeRssProvider(client).CollectAsync("GENSHIN","channel","Genshin Impact");Assert.Single(rows);Assert.Equal("VIDEO",rows[0].ItemType);Assert.Equal("https://www.youtube.com/watch?v=abc",rows[0].Evidence[0].SourceUrl);Assert.Equal("UTC",rows[0].SourceTimezone);Assert.Equal(18,rows[0].NormalizedTime!.Value.Hour);Assert.Equal("UTC",rows[0].Evidence[0].OriginalTimezone);Assert.Equal(18,rows[0].Evidence[0].NormalizedTime!.Value.Hour);}

 [Fact] public async Task CollectAsync_RetriesTransientFailureThenSucceeds(){const string rss="<feed xmlns='http://www.w3.org/2005/Atom' xmlns:yt='http://www.youtube.com/xml/schemas/2015'><title>Genshin Impact</title></feed>";var handler=new SequenceHandler([(HttpStatusCode.ServiceUnavailable,null),(HttpStatusCode.OK,rss)]);using var client=new HttpClient(handler);var rows=await new OfficialYoutubeRssProvider(client).CollectAsync("GENSHIN","channel","Genshin Impact");Assert.Empty(rows);Assert.Equal(2,handler.Calls);}

 [Fact] public async Task CollectAsync_DoesNotRetryMalformedXml(){var handler=new SequenceHandler([new(HttpStatusCode.OK,"not-xml")]);using var client=new HttpClient(handler);await Assert.ThrowsAsync<System.Xml.XmlException>(()=>new OfficialYoutubeRssProvider(client).CollectAsync("GENSHIN","channel","Genshin Impact"));Assert.Equal(1,handler.Calls);}

 [Fact] public async Task CollectAsync_StopsAfterThreeTransientFailures(){var handler=new SequenceHandler([(HttpStatusCode.ServiceUnavailable,null),(HttpStatusCode.ServiceUnavailable,null),(HttpStatusCode.ServiceUnavailable,null),(HttpStatusCode.OK,"unused")]);using var client=new HttpClient(handler);await Assert.ThrowsAsync<HttpRequestException>(()=>new OfficialYoutubeRssProvider(client).CollectAsync("GENSHIN","channel","Genshin Impact"));Assert.Equal(3,handler.Calls);}

 private sealed class H(string value):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(new HttpResponseMessage{Content=new StringContent(value)});}
 private sealed class SequenceHandler((HttpStatusCode Status,string? Body)[] responses):HttpMessageHandler{private int _calls;public int Calls=>_calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){var index=Interlocked.Increment(ref _calls)-1;var item=responses[Math.Min(index,responses.Length-1)];return Task.FromResult(new HttpResponseMessage(item.Status){Content=new StringContent(item.Body??string.Empty)});}}
}
