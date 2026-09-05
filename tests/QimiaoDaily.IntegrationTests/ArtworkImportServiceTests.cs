using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;
using System.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace QimiaoDaily.IntegrationTests;

public sealed class ArtworkImportServiceTests
{
    [Fact]
    public async Task ImportAndSelection_EnforcesPermanentDeduplicationAndReviewGate()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new ArtworkImportService(database); var item = Candidate("1", "https://www.pixiv.net/artworks/1");
        Assert.True(await service.ImportAsync(item, "hash")); Assert.False(await service.ImportAsync(item, "hash"));
        var saved = await database.Artworks.SingleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetSelectedForReportAsync(saved.Id, true));
        await service.ConfirmAsync(saved.Id); await service.SetSelectedForReportAsync(saved.Id, true);
        saved = await database.Artworks.SingleAsync(); Assert.Equal(ReviewStatus.Confirmed, saved.ReviewStatus); Assert.True(saved.SelectedForReport);
        Assert.False(await service.ImportAsync(Candidate("2", "https://www.pixiv.net/artworks/2"), "hash"));
    }
    [Fact]
    public async Task DailyRefresh_ReportsOnlyActuallyNewCandidates()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        const string json = "{\"contents\":[{\"illust_id\":100,\"title\":\"Artwork\",\"user_name\":\"Artist\",\"user_id\":42,\"illust_upload_timestamp\":1786665600,\"url\":\"https://i.pximg.net/t.jpg\"}]}";
        using var client = new HttpClient(new Handler(json)); var refresh = new ArtworkDailyRefreshService(new PixivArtworkProvider(client), new ArtworkImportService(database), database);
        Assert.Equal(1, (await refresh.RefreshAsync()).Imported); Assert.Equal(0, (await refresh.RefreshAsync()).Imported);
        var runs = (await database.ArtworkDailyRuns.ToListAsync()).OrderBy(x => x.CompletedAt).ToList();
        Assert.Equal(2, runs.Count);
        Assert.Equal("PARTIAL", runs[0].Status);
        Assert.Equal("1/30", $"{runs[0].NewCandidateCount}/{runs[0].TargetCount}");
        Assert.Equal(0, runs[1].NewCandidateCount);
    }

    [Fact]
    public async Task DailyRefresh_KeepsNewCandidatesAsOnlinePreviewsUntilSelectedForReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-artwork-preview-" + Guid.NewGuid().ToString("N"));
        var paths = new QimiaoDailyPaths(root); paths.EnsureDirectories();
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
            await using (var database = new QimiaoDailyDbContext(options))
            {
                await database.Database.OpenConnectionAsync();
                await database.Database.EnsureCreatedAsync();
                const string json = "{\"contents\":[{\"illust_id\":100,\"title\":\"Artwork\",\"user_name\":\"Artist\",\"user_id\":42,\"illust_upload_timestamp\":1786665600,\"url\":\"https://i.pximg.net/t.jpg\"}]}";
                var handler = new ThumbnailCountingHandler(json);
                using var client = new HttpClient(handler);
                var refresh = new ArtworkDailyRefreshService(new PixivArtworkProvider(client), new ArtworkImportService(database), database);

                Assert.Equal(1, (await refresh.RefreshAsync(target: 1, rankingLimit: 1)).Imported);
                var item = await database.Artworks.SingleAsync();
                Assert.Equal("https://i.pximg.net/t.jpg", item.ThumbnailUrl);
                Assert.Equal(0, handler.ThumbnailRequests);
                Assert.Empty(Directory.EnumerateFiles(paths.ImagesDirectory));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DailyRefresh_PersistsExplicitFailureReasonAndStatus()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient(new StatusHandler(HttpStatusCode.Forbidden));
        var refresh = new ArtworkDailyRefreshService(new PixivArtworkProvider(client), new ArtworkImportService(database), database);

        var result = await refresh.RefreshAsync();

        Assert.Equal(ArtworkFetchStatus.LoginRequired, result.Status);
        var run = await database.ArtworkDailyRuns.SingleAsync();
        Assert.Equal("LOGIN_REQUIRED", run.Status);
        Assert.Contains("authorized session", run.FailureReason);
        Assert.Equal(0, run.NewCandidateCount);
    }
    [Fact]
    public async Task ReturnToReview_ClearsReportSelection()
    {
        var options=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;await using var database=new QimiaoDailyDbContext(options);await database.Database.OpenConnectionAsync();await database.Database.EnsureCreatedAsync();
        var service=new ArtworkImportService(database);await service.ImportAsync(Candidate("3","https://www.pixiv.net/artworks/3"));var item=await database.Artworks.SingleAsync();await service.ConfirmAsync(item.Id);await service.SetSelectedForReportAsync(item.Id,true);await service.ReturnToReviewAsync(item.Id);
        item=await database.Artworks.SingleAsync();Assert.Equal(ReviewStatus.Pending,item.ReviewStatus);Assert.False(item.SelectedForReport);
    }
    [Fact]
    public async Task ThumbnailCache_PersistsLocalPathAndHashWithoutChangingSourceUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-thumbnail-" + Guid.NewGuid().ToString("N"));
        var paths = new QimiaoDailyPaths(root); paths.EnsureDirectories();
        try
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            using var client = new HttpClient(new BinaryHandler(bytes));
            var item = new ArtworkEntity { Platform = "PIXIV", ArtworkId = "123", Title = "art", Author = "author", SourceUrl = "https://www.pixiv.net/artworks/123", ThumbnailUrl = "https://i.pximg.net/thumb.jpg", PublishedAt = DateTimeOffset.UtcNow, FetchedAt = DateTimeOffset.UtcNow };
            var cache = new ArtworkThumbnailCacheService(client, paths);

            Assert.True(await cache.TryCacheAsync(item));
            Assert.StartsWith(paths.ImagesDirectory, item.ThumbnailUrl, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("https://i.pximg.net/thumb.jpg", item.ThumbnailUrl);
            Assert.NotNull(item.ThumbnailSha256);
            Assert.True(File.Exists(item.ThumbnailUrl));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(item.ThumbnailUrl));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Import_UsesPerceptualHashToRejectDifferentUrlNearDuplicate()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        await using var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(32, 32))
        {
            for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++) image[x, y] = new Rgba32((byte)(x * 7), (byte)(y * 7), 80);
            await image.SaveAsPngAsync(stream);
        }
        var payload = await ArtworkImageFingerprint.TryCreateAsync(stream.ToArray());
        Assert.NotNull(payload);
        Assert.NotNull(payload!.PerceptualHash);
        var service = new ArtworkImportService(database);
        var firstCandidate = Candidate("phash-1", "https://www.pixiv.net/artworks/phash-1") with { CharacterName = "Character", FranchiseName = "GENSHIN", Category = "ILLUST", Tags = "tag", Width = payload.Width, Height = payload.Height, SourceMetadata = "raw" };
        Assert.True(await service.ImportAsync(firstCandidate, payload.ContentSha256, payload.PerceptualHash));
        var saved = await database.Artworks.SingleAsync();
        Assert.Equal(("Character", "GENSHIN", "ILLUST", "tag", "raw"), (saved.CharacterName, saved.FranchiseName, saved.Category, saved.Tags, saved.SourceMetadata));
        Assert.False(await service.ImportAsync(Candidate("phash-2", "https://www.pixiv.net/artworks/phash-2"), "different-content", payload.PerceptualHash));
        Assert.True(await service.ImportAsync(Candidate("phash-3", "https://www.pixiv.net/artworks/phash-3"), "another-content", "AAAAAAAAAAAAAAAA"));
        Assert.Equal(2, await database.SeenArtworks.CountAsync());
    }

    [Fact]
    public async Task Delete_RetainsPermanentSeenArtworkAndWritesAudit()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new ArtworkImportService(database);
        Assert.True(await service.ImportAsync(Candidate("delete-1", "https://www.pixiv.net/artworks/delete-1"), "delete-hash"));
        var item = await database.Artworks.SingleAsync();

        await service.DeleteAsync(item.Id, "tester", "remove rejected candidate", DateTimeOffset.UtcNow);

        Assert.Empty(await database.Artworks.ToListAsync());
        Assert.Single(await database.SeenArtworks.ToListAsync());
        Assert.Contains(await database.ArtworkReviewActions.ToListAsync(), x => x.Action == "DELETE" && x.Reason == "remove rejected candidate");
        Assert.Contains(await database.ArtworkRevisions.ToListAsync(), x => x.ArtworkId == item.Id && x.FieldName == "Artwork");
        Assert.False(await service.ImportAsync(Candidate("delete-1", "https://www.pixiv.net/artworks/delete-1"), "new-hash"));
    }

    [Fact]
    public async Task EditMetadata_WritesFieldRevisionsAndRequiresReason()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new ArtworkImportService(database);
        Assert.True(await service.ImportAsync(Candidate("edit-1", "https://www.pixiv.net/artworks/edit-1")));
        var item = await database.Artworks.SingleAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.EditMetadataAsync(item.Id, "new title", "", "GENSHIN", "ILLUST", "tag", "tester", "", DateTimeOffset.UtcNow));
        var changed = await service.EditMetadataAsync(item.Id, "new title", "Lumine", "GENSHIN", "ILLUST", "tag", "tester", "correct metadata", DateTimeOffset.UtcNow);

        Assert.Equal(5, changed);
        var saved = await database.Artworks.SingleAsync();
        Assert.Equal(("new title", "Lumine", "GENSHIN", "ILLUST", "tag"), (saved.Title, saved.CharacterName, saved.FranchiseName, saved.Category, saved.Tags));
        Assert.Equal(5, await database.ArtworkRevisions.CountAsync(x => x.ArtworkId == item.Id));
        Assert.Contains(await database.ArtworkReviewActions.ToListAsync(), x => x.Action == "EDIT" && x.Reason == "correct metadata");
    }

    [Fact]
    public async Task BatchOperations_OnlyMoveApplicableReviewStates()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        var service = new ArtworkImportService(database);
        Assert.True(await service.ImportAsync(Candidate("batch-1", "https://www.pixiv.net/artworks/batch-1")));
        Assert.True(await service.ImportAsync(Candidate("batch-2", "https://www.pixiv.net/artworks/batch-2")));
        var ids = await database.Artworks.Select(x => x.Id).ToListAsync();

        Assert.Equal(2, await service.BatchConfirmAsync(ids, "tester", "batch confirm", DateTimeOffset.UtcNow));
        Assert.Equal(1, await service.BatchReturnToReviewAsync(ids.Take(1), "tester", "batch return", DateTimeOffset.UtcNow));
        Assert.Equal(1, await service.BatchConfirmAsync(ids.Take(1), "tester", "confirm returned", DateTimeOffset.UtcNow));
        Assert.Equal(0, await service.BatchConfirmAsync(ids.Skip(1), "tester", "already confirmed", DateTimeOffset.UtcNow));
        Assert.Equal(1, await service.BatchDeleteAsync(ids.Take(1), "tester", "batch delete", DateTimeOffset.UtcNow));
        Assert.Single(await database.Artworks.ToListAsync());
        Assert.Equal(2, await database.SeenArtworks.CountAsync());
    }

    [Fact]
    public async Task LiveDailyRefresh_UsesRealPixivSourceWhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS") != "1") return;
        var root = Path.Combine(Path.GetTempPath(), "qimiao-live-artwork-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new QimiaoDailyPaths(root); paths.EnsureDirectories();
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={paths.DatabasePath}").Options;
            await using var database = new QimiaoDailyDbContext(options);
            await database.Database.EnsureCreatedAsync();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var refresh = new ArtworkDailyRefreshService(new PixivArtworkProvider(client), new ArtworkImportService(database), database);
            var result = await refresh.RefreshAsync(1);
            Assert.Equal(ArtworkFetchStatus.Healthy, result.Status);
            Assert.True(result.Fetched > 0);
            Assert.Single(await database.ArtworkDailyRuns.ToListAsync());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
    [Fact]
    public async Task DailyRefresh_UsesConfiguredDirectArtworkWhenRankingIsUnavailable()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options); await database.Database.OpenConnectionAsync(); await database.Database.EnsureCreatedAsync();
        using var client = new HttpClient(new RankingBlockedDirectArtworkHandler());
        var refresh = new ArtworkDailyRefreshService(new PixivArtworkProvider(client), new ArtworkImportService(database), database);

        var result = await refresh.RefreshAsync(target: 1, rankingLimit: 1, directArtworkIds: ["100000000"]);

        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status);
        Assert.Equal(1, result.Imported);
        Assert.Contains("configured direct artwork IDs", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OfficialArtworkCandidate Candidate(string id, string url) => new("PIXIV", id, "title", "author", "a", url, "https://i.pximg.net/t.jpg", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    private sealed class Handler(string content) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) }); }
    private sealed class ThumbnailCountingHandler(string content) : HttpMessageHandler
    {
        public int ThumbnailRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host.Equals("i.pximg.net", StringComparison.OrdinalIgnoreCase) == true) ThumbnailRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }
    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status)); }
    private sealed class RankingBlockedDirectArtworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("ranking.php", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            const string json = "{\"error\":false,\"body\":{\"title\":\"Configured artwork\",\"userName\":\"Artist\",\"userId\":\"42\",\"createDate\":\"2026-08-20T01:00:00+00:00\",\"urls\":{\"thumb\":\"https://i.pximg.net/thumb.jpg\"}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
    private sealed class BinaryHandler(byte[] content) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }); }
}
