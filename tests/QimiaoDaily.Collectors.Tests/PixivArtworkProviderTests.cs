using System.Net;
using System.Text;
using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class PixivArtworkProviderTests
{
    [Fact]
    public async Task FetchAsync_ParsesPlatformMetadataWithoutSearchApi()
    {
        const string json = "{\"error\":false,\"body\":{\"title\":\"Artwork\",\"userName\":\"Artist\",\"userId\":\"42\",\"createDate\":\"2026-08-14T01:02:03+00:00\",\"urls\":{\"thumb_mini\":\"https://i.pximg.net/thumb.jpg\"}}}";
        using var client = new HttpClient(new Handler(HttpStatusCode.OK, json));
        var result = await new PixivArtworkProvider(client).FetchAsync("100000000");
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status); Assert.NotNull(result.Candidate); Assert.Equal("https://www.pixiv.net/artworks/100000000", result.Candidate!.SourceUrl); Assert.Equal("Artist", result.Candidate.Author);
    }
    [Fact]
    public async Task FetchAsync_ReportsLoginRequiredInsteadOfEmptySuccess()
    {
        using var client = new HttpClient(new Handler(HttpStatusCode.Forbidden, ""));
        var result = await new PixivArtworkProvider(client).FetchAsync("100000000");
        Assert.Equal(ArtworkFetchStatus.LoginRequired, result.Status); Assert.Null(result.Candidate);
    }
    [Fact]
    public async Task FetchDailyRankingAsync_ParsesDirectPixivRankingCandidates()
    {
        const string json = "{\"contents\":[{\"illust_id\":100,\"title\":\"Artwork\",\"user_name\":\"Artist\",\"user_id\":42,\"illust_upload_timestamp\":1786665600,\"url\":\"https://i.pximg.net/t.jpg\"}]}";
        using var client = new HttpClient(new Handler(HttpStatusCode.OK, json)); var result = await new PixivArtworkProvider(client).FetchDailyRankingAsync();
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status); Assert.Single(result.Candidates); Assert.Equal("100", result.Candidates[0].ArtworkId);
    }

    [Fact]
    public async Task FetchDailyRankingAsync_PreservesAvailableMetadataWithoutGuessingMissingFields()
    {
        const string json = "{\"contents\":[{\"illust_id\":100,\"title\":\"Artwork\",\"user_name\":\"Artist\",\"user_id\":42,\"illust_upload_timestamp\":1786665600,\"url\":\"https://i.pximg.net/t.jpg\",\"type\":\"illust\",\"tags\":[{\"name\":\"genshin\"}],\"width\":1200,\"height\":800}]}";
        using var client = new HttpClient(new Handler(HttpStatusCode.OK, json));
        var result = await new PixivArtworkProvider(client).FetchDailyRankingAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("illust", candidate.Category);
        Assert.Equal("genshin", candidate.Tags);
        Assert.Equal((1200, 800), (candidate.Width, candidate.Height));
        Assert.Null(candidate.CharacterName);
        Assert.Contains("illust_id", candidate.SourceMetadata);
    }

    [Fact]
    public async Task SearchAsync_AssignsTheRequestedCharacterAndFranchise()
    {
        const string json = "{\"error\":false,\"body\":{\"illustManga\":{\"data\":[{\"id\":\"100\",\"title\":\"Artwork\",\"userName\":\"Artist\",\"userId\":\"42\",\"url\":\"https://i.pximg.net/t.jpg\",\"createDate\":\"2026-08-14T01:02:03+00:00\",\"illustType\":\"illust\",\"tags\":[\"tag\"],\"width\":1200,\"height\":800}]}}}";
        using var client = new HttpClient(new Handler(HttpStatusCode.OK, json));
        var result = await new PixivArtworkProvider(client).SearchAsync(new("\u80e1\u6843", "GENSHIN", "\u80e1\u6843 \u539f\u795e"));

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status);
        Assert.Equal(("\u80e1\u6843", "GENSHIN", "tag"), (candidate.CharacterName, candidate.FranchiseName, candidate.Tags));
        Assert.Equal("https://www.pixiv.net/artworks/100", candidate.SourceUrl);
    }
    [Fact]
    public async Task FetchAsync_SendsConfiguredSessionCookieWithoutPersistingItInCandidate()
    {
        const string json = "{\"error\":false,\"body\":{\"title\":\"Artwork\",\"userName\":\"Artist\",\"userId\":\"42\",\"createDate\":\"2026-08-14T01:02:03+00:00\",\"urls\":{\"thumb\":\"https://i.pximg.net/thumb.jpg\"}}}";
        var handler = new CookieHandler(json);
        using var client = new HttpClient(handler);
        var result = await new PixivArtworkProvider(client, "PHPSESSID=encrypted-session").FetchAsync("100000000");
        Assert.Equal(ArtworkFetchStatus.Healthy, result.Status);
        Assert.Equal("PHPSESSID=encrypted-session", handler.Cookie);
        Assert.DoesNotContain("PHPSESSID", result.Candidate!.SourceUrl);
    }
    private sealed class Handler(HttpStatusCode code, string content) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(content, Encoding.UTF8, "application/json") }); }
    private sealed class CookieHandler(string content) : HttpMessageHandler
    {
        public string? Cookie { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Cookie = request.Headers.GetValues("Cookie").SingleOrDefault();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
