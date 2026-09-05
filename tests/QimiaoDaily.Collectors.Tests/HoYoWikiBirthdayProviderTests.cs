using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class HoYoWikiBirthdayProviderTests
{
    [Fact]
    public async Task CollectAsync_ParsesStructuredOfficialBirthdayField()
    {
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Kamisato Ayaka\",\"modules\":[{\"components\":[{\"data\":\"{\\\"key\\\":\\\"Birthday\\\",\\\"value\\\":[\\\"9/28\\\"]}\"}]}]}}}";
        using var client = new HttpClient(new Handler(body));
        var candidate = await new HoYoWikiBirthdayProvider(client).CollectAsync(32);
        Assert.Equal("Kamisato Ayaka", candidate.Character); Assert.Equal((9, 28), (candidate.Month, candidate.Day)); Assert.Equal("GENSHIN", candidate.Franchise); Assert.Contains("Birthday: 9/28", candidate.Evidence);
    }

    [Fact]
    public async Task CollectAsync_PreservesUnknownWhenOfficialPageHasNoBirthdayField()
    {
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Unknown Character\",\"modules\":[]}}}";
        using var client = new HttpClient(new Handler(body));
        var candidate = await new HoYoWikiBirthdayProvider(client).CollectAsync(99);
        Assert.True(candidate.IsUnknown);
        Assert.Equal((0, 0), (candidate.Month, candidate.Day));
        Assert.Contains("UNKNOWN", candidate.Evidence);
    }

    [Fact]
    public async Task CollectAsync_PreservesUnknownWhenOfficialPageUsesDashPlaceholder()
    {
        const string body = "{\"retcode\":0,\"data\":{\"page\":{\"name\":\"Traveler (Geo)\",\"modules\":[{\"components\":[{\"data\":\"{\\\"key\\\":\\\"Birthday\\\",\\\"value\\\":[\\\"<p>-</p>\\\"]}\"}]}]}}}";
        using var client = new HttpClient(new Handler(body));
        var candidate = await new HoYoWikiBirthdayProvider(client).CollectAsync(17);
        Assert.True(candidate.IsUnknown);
        Assert.Equal((0, 0), (candidate.Month, candidate.Day));
        Assert.Contains("<p>-</p>", candidate.Evidence);
    }

    private sealed class Handler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
    }
}
