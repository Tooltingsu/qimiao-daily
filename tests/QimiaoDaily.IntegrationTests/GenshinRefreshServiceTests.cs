using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class GenshinRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_WritesOfficialCandidatesAsPending_WithEvidence()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string list = "{\"data\":{\"list\":[{\"list\":[{\"ann_id\":\"700\",\"title\":\"\u9650\u65f6\u6d3b\u52a8\",\"start_time\":\"2026-08-20 10:00:00\",\"end_time\":\"2026-08-30 10:00:00\",\"type\":1}]}]}}";
        const string detail = "{\"data\":{\"list\":[{\"ann_id\":\"700\",\"content\":\"\u6d3b\u52a8\u65f6\u95f4\"}]}}";
        using var client = new HttpClient(new Handler(list, detail));
        var service = new GenshinRefreshService(database, new GenshinAnnouncementProvider(client));

        Assert.Equal(1, await service.RefreshAsync());
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleAsync();
        Assert.Equal(ReviewStatus.Pending, item.ReviewStatus);
        Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);
        Assert.Null(item.NormalizedTime);
        Assert.Equal(2, item.Evidence.Count);
        Assert.Equal(0, await service.RefreshAsync());
    }

    [Fact]
    public async Task RefreshAsync_CreatesPendingVersionWhenStableIdentityChangesAndSkipsRepeat()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string listV1 = "{\"data\":{\"list\":[{\"list\":[{\"ann_id\":\"701\",\"title\":\"限时活动\",\"start_time\":\"2026-08-20 10:00:00\",\"end_time\":\"2026-08-30 10:00:00\",\"type\":1}]}]}}";
        const string detailV1 = "{\"data\":{\"list\":[{\"ann_id\":\"701\",\"content\":\"活动时间\"}]}}";
        const string listV2 = "{\"data\":{\"list\":[{\"list\":[{\"ann_id\":\"701\",\"title\":\"限时活动\",\"start_time\":\"2026-08-21 10:00:00\",\"end_time\":\"2026-08-31 10:00:00\",\"type\":1}]}]}}";
        const string detailV2 = "{\"data\":{\"list\":[{\"ann_id\":\"701\",\"content\":\"活动时间更新\"}]}}";
        using var client = new HttpClient(new ChangingHandler(listV1, detailV1, listV2, detailV2));
        var service = new GenshinRefreshService(database, new GenshinAnnouncementProvider(client));

        Assert.Equal(1, await service.RefreshAsync());
        Assert.Equal(1, await service.RefreshAsync());
        Assert.Equal(0, await service.RefreshAsync());

        var items = (await database.TimelineItems.ToListAsync()).OrderBy(x => x.FetchedAt).ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("GENSHIN:701", items[0].CanonicalIdentity);
        Assert.Equal(TimelineChangeKind.New, items[0].ChangeKind);
        Assert.Equal(TimelineChangeKind.TimeChanged, items[1].ChangeKind);
        Assert.All(items, x => Assert.Equal(ReviewStatus.Pending, x.ReviewStatus));
    }

    private sealed class Handler(string list,string detail):HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(r.RequestUri!.ToString().Contains("getAnnList")?list:detail,Encoding.UTF8,"application/json")}); }
    private sealed class ChangingHandler(string listV1, string detailV1, string listV2, string detailV2) : HttpMessageHandler
    {
        private int _calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var version = (Interlocked.Increment(ref _calls) + 1) / 2 >= 2 ? 2 : 1;
            var isList = request.RequestUri!.ToString().Contains("getAnnList", StringComparison.Ordinal);
            var body = version == 1 ? (isList ? listV1 : detailV1) : (isList ? listV2 : detailV2);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
