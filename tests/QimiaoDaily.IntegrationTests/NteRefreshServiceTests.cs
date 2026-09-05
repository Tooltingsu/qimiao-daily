using System.Net;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;
namespace QimiaoDaily.IntegrationTests;
public sealed class NteRefreshServiceTests
{
 [Fact] public async Task ImportVerifiedVideoAsync_WritesPendingOfficialVideo(){var opt=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;await using var db=new QimiaoDailyDbContext(opt);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();using var client=new HttpClient(new H());var s=new NteRefreshService(db,new NteBilibiliOfficialProvider(client));Assert.True(await s.ImportVerifiedVideoAsync("BVtest"));var item=await db.TimelineItems.Include(x=>x.Evidence).SingleAsync();Assert.Equal("NTE",item.GameCode);Assert.Equal(ReviewStatus.Pending,item.ReviewStatus);Assert.Single(item.Evidence);}
 private sealed class H:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":0,\"data\":{\"title\":\"official video\",\"desc\":\"source\",\"pubdate\":1786590000,\"owner\":{\"mid\":3546636978489848}}}")});}
}
