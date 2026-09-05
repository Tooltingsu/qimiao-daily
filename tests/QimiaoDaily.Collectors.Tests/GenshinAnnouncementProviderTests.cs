using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class GenshinAnnouncementProviderTests
{
    [Fact]
    public async Task CollectAsync_ParsesOfficialActivityAndGacha_WithEvidence()
    {
        const string list = "{\"data\":{\"list\":[{\"list\":[{\"ann_id\":\"101\",\"title\":\"\u9650\u65f6\u6d3b\u52a8\u6d4b\u8bd5\",\"start_time\":\"2026-08-20 10:00:00\",\"end_time\":\"2026-08-30 10:00:00\",\"type\":1},{\"ann_id\":\"102\",\"title\":\"\u6982\u7387UP\u7948\u613f\u6d4b\u8bd5\",\"start_time\":\"2026-08-21 18:00:00\",\"end_time\":\"2026-09-01 18:00:00\",\"type\":1},{\"ann_id\":\"103\",\"title\":\"\u95ee\u5377\u8c03\u67e5\",\"start_time\":\"2026-08-21 18:00:00\",\"end_time\":\"2026-09-01 18:00:00\",\"type\":1},{\"ann_id\":\"104\",\"title\":\"\u6df1\u5883\u87ba\u65cb\u5468\u671f\",\"start_time\":\"2026-08-16 04:00:00\",\"end_time\":\"2026-09-01 04:00:00\",\"type\":0}]}]}}";
        const string content = "{\"data\":{\"list\":[{\"ann_id\":\"101\",\"content\":\"活动时间 2026/08/20\"},{\"ann_id\":\"102\",\"content\":\"祈愿介绍\"},{\"ann_id\":\"103\",\"content\":\"活动时间\"},{\"ann_id\":\"104\",\"content\":\"深境螺旋周期\"}]}}";
        using var client = new HttpClient(new StubHandler(list, content));

        var candidates = await new GenshinAnnouncementProvider(client).CollectAsync();

        Assert.Equal(3, candidates.Count);
        Assert.Contains(candidates, x => x.ItemType == "EVENT");
        Assert.Contains(candidates, x => x.ItemType == "GACHA");
        Assert.Contains(candidates, x => x.ItemType == "ENDGAME");
        Assert.All(candidates, x => Assert.Equal(2, x.Evidence.Count));
        var activity = Assert.Single(candidates, x => x.ExternalId == "101");
        Assert.Equal("activity-body", activity.StartTimeSource);
        Assert.Equal(20, activity.NormalizedTime!.Value.Day);
        Assert.Equal(4, activity.SourceCandidateCount);
        Assert.Equal(1, activity.SourceRejectedCount);
        Assert.Equal(1, activity.SourceRejectionReasons["ignored_rule"]);
    }

    [Fact]
    public async Task CollectAsync_PersistsStructuredGachaFields()
    {
        const string list = "{\"data\":{\"list\":[{\"list\":[{\"ann_id\":\"201\",\"title\":\"集录祈愿·上半\",\"start_time\":\"2026-08-20 10:00:00\",\"end_time\":\"2026-08-30 10:00:00\",\"type\":1}]}]}}";
        const string content = "{\"data\":{\"list\":[{\"ann_id\":\"201\",\"content\":\"本期集录祈愿，上半开启。\"}]}}";
        using var client = new HttpClient(new StubHandler(list, content));

        var candidate = Assert.Single(await new GenshinAnnouncementProvider(client).CollectAsync());

        Assert.Equal("CHRONICLED", candidate.GachaPoolKind);
        Assert.Equal("FIRST_HALF", candidate.GachaPoolPhase);
    }

    private sealed class StubHandler(string list, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.ToString().Contains("getAnnList") ? list : content, Encoding.UTF8, "application/json") });
    }
}
