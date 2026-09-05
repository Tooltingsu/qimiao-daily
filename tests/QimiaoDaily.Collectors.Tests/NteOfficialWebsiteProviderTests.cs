using System.Net;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class NteOfficialWebsiteProviderTests
{
    [Fact]
    public async Task CollectAsync_ParsesFirstPartyEventIndexAndExcludesBroadNotices()
    {
        using var client = new HttpClient(new Handler("""
            var newsdataObj = {
              "cn": { "news": [
                {"title":"官方活动","url":"/cn/article/news/gameevent/20260701/1.html","time":"2026-07-01","channelDescription":"活动","channelName":"gameevent"},
                {"title":"违规公告","url":"/cn/article/news/gamebroad/20260702/2.html","time":"2026-07-02","channelDescription":"公告","channelName":"gamebroad"}
              ] },
              "de": { "news": [] }
            };
            """));

        var result = await new NteOfficialWebsiteProvider(client).CollectAsync();

        Assert.Equal(SourceFetchStatus.Healthy, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("NTE", candidate.GameCode);
        Assert.Equal("EVENT", candidate.ItemType);
        Assert.Equal("2026-07-01", candidate.SourceTime);
        Assert.Equal("https://nte.perfectworld.com/cn/article/news/gameevent/20260701/1.html", candidate.Evidence.Single().SourceUrl);
        Assert.Equal("NteOfficialWebsite", candidate.Evidence.Single().Provider);
        Assert.Equal("2026-07-01", candidate.Evidence.Single().PublishedAt!.Value.ToString("yyyy-MM-dd"));
        Assert.Null(candidate.NormalizedTime);
        Assert.Equal(2, result.SourceCandidateCount);
        Assert.Equal(1, result.SourceRejectedCount);
        Assert.Equal(1, result.SourceRejectionReasons!["unsupported_channel"]);
    }

    [Fact]
    public async Task CollectVideosAsync_ParsesOfficialMp4AssetsWithoutGuessingDates()
    {
        using var client = new HttpClient(new Handler("https://ntevmg.perfectworld.com/webops/nte/nte_260708_shinku.mp4"));
        var candidates = await new NteOfficialWebsiteProvider(client).CollectVideosAsync();
        var candidate = Assert.Single(candidates);
        Assert.Equal("VIDEO", candidate.ItemType);
        Assert.Null(candidate.NormalizedTime);
        Assert.EndsWith("nte_260708_shinku.mp4", candidate.Evidence.Single().SourceUrl);
    }

    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
