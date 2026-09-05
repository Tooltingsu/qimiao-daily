using System.Net;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class BgiRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_AutomaticallySelectsCurrentWindowAndDeduplicates()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        database.GitCommitRecords.Add(new GitCommitRecord
        {
            Repository = "a/b",
            Sha = "stale",
            Subject = "stale commit",
            Url = "https://github.com/a/b/commit/stale",
            FetchedAt = DateTimeOffset.UtcNow,
            SelectedForReport = true
        });
        await database.SaveChangesAsync();

        using var client = new HttpClient(new Handler());
        var service = new BgiRefreshService(database, new GitHubCommitProvider(client));

        await service.RefreshAsync("a/b", DateTimeOffset.UtcNow);
        Assert.Equal(0, await service.RefreshAsync("a/b", DateTimeOffset.UtcNow));

        var records = await database.GitCommitRecords.OrderBy(x => x.Sha).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.False(records.Single(x => x.Sha == "stale").SelectedForReport);
        Assert.True(records.Single(x => x.Sha == "abc").SelectedForReport);
    }

    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"sha\":\"abc\",\"html_url\":\"https://github.com/a/b/commit/abc\",\"commit\":{\"message\":\"s\",\"author\":{\"name\":\"a\",\"date\":\"2026-08-13T10:00:00Z\"},\"committer\":{\"name\":\"a\",\"date\":\"2026-08-13T10:00:00Z\"}}}]")
            });
    }
}
