using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class NteGameBirthdayProviderTests
{
    [Fact]
    public async Task CollectAsync_ParsesExplicitDatesAndDeduplicatesRscEntries()
    {
        const string body = "{\"Name\":\"Hotori\",\"Birthday\":\"December 20\"}" +
            "{\"Name\":\"Hotori\",\"Birthday\":\"December 20\"}" +
            "{\"Name\":\"Zero\",\"Birthday\":\"unknown\"}";
        using var client = new HttpClient(new Handler(body));
        var rows = await new NteGameBirthdayProvider(client).CollectAsync();
        var row = Assert.Single(rows);
        Assert.Equal("Hotori", row.Character);
        Assert.Equal((12, 20), (row.Month, row.Day));
        Assert.Equal("NTEGame", row.Provider);
        Assert.Contains("non-official", row.EvidenceExcerpt, StringComparison.Ordinal);
    }

    private sealed class Handler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "text/html") });
    }
}
