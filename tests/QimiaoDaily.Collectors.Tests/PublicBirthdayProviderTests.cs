using System.Net;

namespace QimiaoDaily.Collectors.Tests;

public sealed class PublicBirthdayProviderTests
{
    [Fact]
    public async Task BiligameProvider_ParsesStructuredBirthdayAndKeepsUnknownRows()
    {
        using var client = new HttpClient(new RouteHandler(request =>
            request.RequestUri!.AbsoluteUri.Contains("%E7%90%AA%E4%BA%9A", StringComparison.Ordinal)
                ? "<table><tr><th>生日</th><td>1998年12月07日</td></tr></table>"
                : "<table><tr><th>生日</th><td>不明</td></tr></table>"));
        var provider = new BiligameBirthdayProvider(client);

        var rows = await provider.CollectAsync(["Kiana Kaslana", "Unknown"]);

        Assert.Equal((12, 7), (rows[0].Month, rows[0].Day));
        Assert.Equal((0, 0), (rows[1].Month, rows[1].Day));
        Assert.All(rows, row => Assert.Equal("Biligame HI3 Wiki", row.Provider));
    }

    [Fact]
    public async Task BiligameProvider_RetriesTransientResponseBeforeMarkingBirthdayUnknown()
    {
        var attempts = 0;
        using var client = new HttpClient(new HttpMessageHandlerStub(_ =>
        {
            attempts++;
            return attempts == 1
                ? new HttpResponseMessage((HttpStatusCode)567)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<table><tr><th>生日</th><td>1998年12月07日</td></tr></table>")
                };
        }));
        var provider = new BiligameBirthdayProvider(client);

        var row = Assert.Single(await provider.CollectAsync(["Kiana Kaslana"]));

        Assert.Equal(2, attempts);
        Assert.Equal((12, 7), (row.Month, row.Day));
    }

    [Fact]
    public async Task BaiduProvider_ParsesBirthdayInfoboxValue()
    {
        using var client = new HttpClient(new RouteHandler(_ => "{\"key\":\"birthday\",\"text\":\"12月7日\"}"));
        var provider = new BaiduBirthdayProvider(client);

        var row = Assert.Single(await provider.CollectAsync(["Kiana Kaslana"]));

        Assert.Equal((12, 7), (row.Month, row.Day));
        Assert.Equal("Baidu Baike", row.Provider);
        Assert.Contains("baike.baidu.com/item", row.SourceUrl);
    }

    [Fact]
    public async Task MoegirlProvider_ParsesBirthdayFieldAndKeepsEvidenceUrl()
    {
        using var client = new HttpClient(new RouteHandler(_ =>
            "<div>性别 女 生日 1998年12月7日 [6] 配音</div>"));
        var provider = new MoegirlBirthdayProvider(client);

        var row = Assert.Single(await provider.CollectAsync(["Kiana Kaslana"]));

        Assert.Equal((12, 7), (row.Month, row.Day));
        Assert.Equal("Moegirl HI3 Wiki", row.Provider);
        Assert.Contains("mzh.moegirl.org.cn", row.SourceUrl);
        Assert.Contains("1998年12月7日", row.EvidenceExcerpt);
    }

    private sealed class RouteHandler(Func<HttpRequestMessage, string> bodyFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bodyFactory(request)) });
    }

    private sealed class HttpMessageHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
