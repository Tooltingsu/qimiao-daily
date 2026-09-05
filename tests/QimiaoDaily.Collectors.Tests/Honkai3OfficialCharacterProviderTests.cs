using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class Honkai3OfficialCharacterProviderTests
{
    [Fact]
    public async Task CollectAsync_ReadsOfficialCharacterNamesAndKeepsBirthdayUnknown()
    {
        const string body = "{\"retcode\":0,\"data\":{\"list\":[" +
            "{\"sExt\":\"{\\\"520_0\\\":\\\"Kiana Kaslana\\\",\\\"520_1\\\":\\\"Kiana Kaslana\\\"}\"}," +
            "{\"sExt\":\"{\\\"520_0\\\":\\\"Raiden Mei\\\"}\"}," +
            "{\"sExt\":\"{\\\"520_0\\\":\\\"Kiana Kaslana\\\"}\"}]}}";
        using var client = new HttpClient(new Handler(body));
        var candidates = await new Honkai3OfficialCharacterProvider(client).CollectAsync();

        Assert.Equal(["Kiana Kaslana", "Raiden Mei"], candidates.Select(x => x.Character).ToArray());
        Assert.All(candidates, candidate =>
        {
            Assert.Equal("HI3", candidate.Franchise);
            Assert.Equal((0, 0), (candidate.Month, candidate.Day));
            Assert.True(candidate.IsUnknown);
            Assert.Contains("UNKNOWN", candidate.Evidence);
            Assert.Contains("getContentList", candidate.SourceUrl, StringComparison.Ordinal);
            Assert.Contains(Honkai3OfficialCharacterProvider.SourcePageUrl, candidate.Evidence, StringComparison.Ordinal);
        });
    }

    private sealed class Handler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
    }
}
