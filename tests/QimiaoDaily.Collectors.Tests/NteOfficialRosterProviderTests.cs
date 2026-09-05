using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class NteOfficialRosterProviderTests
{
    [Fact]
    public async Task CollectAsync_ReturnsSixteenOfficialSlotsAsUnknown()
    {
        const string html = "<script>yi zhen ka an xun zero-male zero-female mint nanally xiaozhi jiuyuan hasuoer baicang fadia dfde zaowu</script>";
        using var client = new HttpClient(new Handler(html, HttpStatusCode.OK));
        var candidates = await new NteOfficialRosterProvider(client).CollectAsync();

        Assert.Equal(16, candidates.Count);
        Assert.Equal("NTE", candidates[0].Franchise);
        Assert.Equal("官方角色槽位 01", candidates[0].Character);
        Assert.Equal((0, 0), (candidates[0].Month, candidates[0].Day));
        Assert.Equal(
            Enumerable.Range(1, 16).Select(index => $"官方角色槽位 {index:00}"),
            candidates.Select(candidate => candidate.Character));
        var aliases = new[]
        {
            "yi", "zhen", "ka", "an", "xun", "zero-male", "zero-female", "mint",
            "nanally", "xiaozhi", "jiuyuan", "hasuoer", "baicang", "fadia", "dfde", "zaowu"
        };
        Assert.All(candidates.Select((candidate, index) => (candidate, index)), item =>
        {
            Assert.True(item.candidate.IsUnknown);
            Assert.Contains($"Official NTE roster slot: {aliases[item.index]}", item.candidate.Evidence, StringComparison.Ordinal);
            Assert.Contains("birthday field unavailable; UNKNOWN", item.candidate.Evidence, StringComparison.Ordinal);
            Assert.Equal(NteOfficialRosterProvider.MainPageUrl, item.candidate.SourceUrl);
        });
    }

    [Fact]
    public async Task CollectAsync_UsesAuditedRosterWhenOfficialPageTimesOut()
    {
        using var client = new HttpClient(new Handler(string.Empty, HttpStatusCode.GatewayTimeout));
        var candidates = await new NteOfficialRosterProvider(client).CollectAsync();
        Assert.Equal(16, candidates.Count);
        Assert.All(candidates, candidate => Assert.True(candidate.IsUnknown));
    }

    [Fact]
    public async Task CollectAsync_DoesNotPersistAnyPartialHtmlRoster()
    {
        using var client = new HttpClient(new Handler("<script>yi zhen</script>", HttpStatusCode.OK));
        var candidates = await new NteOfficialRosterProvider(client).CollectAsync();
        Assert.Equal(16, candidates.Count);
    }

    private sealed class Handler(string response, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "text/html") });
    }
}
