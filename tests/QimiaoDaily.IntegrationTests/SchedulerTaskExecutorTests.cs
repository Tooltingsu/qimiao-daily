using System.Net;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class SchedulerTaskExecutorTests
{
    [Theory]
    [InlineData("game_data_refresh")]
    [InlineData("birthday_character_refresh")]
    [InlineData("endgame_refresh")]
    [InlineData("nte_official_refresh")]
    public async Task RetiredAutomaticTask_IsRejectedBySchedulerExecutor(string taskKey)
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths(Path.Combine(Path.GetTempPath(), "qimiao-executor-" + Guid.NewGuid().ToString("N"))))
                .ExecuteAsync(taskKey));

        Assert.Contains("retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArtworkRefresh_FailureIsPersistedAndDoesNotReturnSuccess()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var root = Path.Combine(Path.GetTempPath(), "qimiao-pixiv-health-" + Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new PixivBlockedHandler());

        var executor = new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths(root));
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync("artwork_daily_search"));

        var health = await database.ProviderHealthRecords.SingleAsync(x => x.ProviderName == "Pixiv");
        Assert.Equal("LOGIN_REQUIRED", health.Status);
        Assert.Contains("authorized session", health.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PixivBlockedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
    }

}
