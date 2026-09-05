using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors.Tests;

public sealed class ThirdPartyBirthdayProviderTests
{
    [Fact]
    public void MatchingThirdPartySourcesBecomeMultiSourceNotOfficial()
    {
        var candidate = ThirdPartyBirthdayProvider.Merge(
        [
            new BirthdaySource("托马", "GENSHIN", 1, 9, "BWiki", "https://example.invalid/a"),
            new BirthdaySource("Thoma", "GENSHIN", 1, 9, "WikiMirror", "https://example.invalid/b", "托马")
        ]);

        Assert.Equal("托马", candidate.CanonicalCharacterNameZhCn);
        Assert.Equal(VerificationStatus.VerifiedMultiSource, candidate.VerificationStatus);
        Assert.NotEqual(VerificationStatus.VerifiedOfficial, candidate.VerificationStatus);
        Assert.Equal(2, candidate.Sources.Count);
    }

    [Fact]
    public void MergeByCanonicalCharacter_CrossProviderMatchesBecomeVerifiedAndConflictsStayUnverified()
    {
        var candidates = ThirdPartyBirthdayProvider.MergeByCanonicalCharacter(
        [
            new BirthdaySource("Hotori", "NTE", 12, 20, "NTEGame", "https://ntegame.example/hotori"),
            new BirthdaySource("Hotori", "NTE", 12, 20, "NTE Fandom Character Infobox", "https://fandom.example/hotori"),
            new BirthdaySource("Edgar", "NTE", 10, 7, "NTEGame", "https://ntegame.example/edgar"),
            new BirthdaySource("Edgar", "NTE", 10, 31, "NTE Fandom Character Infobox", "https://fandom.example/edgar")
        ]);

        Assert.Equal(2, candidates.Count);
        var hotori = Assert.Single(candidates, x => x.CanonicalCharacterNameZhCn == "Hotori");
        Assert.Equal(VerificationStatus.VerifiedMultiSource, hotori.VerificationStatus);
        Assert.Equal(2, hotori.Sources.Count);
        var edgar = Assert.Single(candidates, x => x.CanonicalCharacterNameZhCn == "Edgar");
        Assert.Equal((0, 0), (edgar.Month, edgar.Day));
        Assert.Equal(VerificationStatus.Unverified, edgar.VerificationStatus);
    }

    [Fact]
    public void ConflictingThirdPartyDatesRemainUnverified()
    {
        var candidate = ThirdPartyBirthdayProvider.Merge(
        [
            new BirthdaySource("北斗", "GENSHIN", 2, 14, "BWiki", "https://example.invalid/a"),
            new BirthdaySource("Beidou", "GENSHIN", 2, 15, "WikiMirror", "https://example.invalid/b", "北斗")
        ]);

        Assert.Equal(VerificationStatus.Unverified, candidate.VerificationStatus);
        Assert.Contains("冲突", candidate.Evidence);
    }

    [Fact]
    public void KnownDateWithUnavailableSecondSourceIsRetainedForReview()
    {
        var candidate = ThirdPartyBirthdayProvider.Merge(
        [
            new BirthdaySource("Kiana Kaslana", "HI3", 12, 7, "Biligame", "https://example.invalid/a"),
            new BirthdaySource("琪亚娜·卡斯兰娜", "HI3", 0, 0, "Baidu", "https://example.invalid/b", "琪亚娜·卡斯兰娜", "HTTP 403; UNKNOWN")
        ]);

        Assert.Equal((12, 7), (candidate.Month, candidate.Day));
        Assert.Equal(VerificationStatus.Unverified, candidate.VerificationStatus);
    }

    [Fact]
    public void MatchingDatedSourcesRemainMultiSourceWhenAnotherSourceIsUnavailable()
    {
        var candidate = ThirdPartyBirthdayProvider.Merge(
        [
            new BirthdaySource("Kiana Kaslana", "HI3", 12, 7, "Biligame", "https://example.invalid/a"),
            new BirthdaySource("Kiana Kaslana", "HI3", 12, 7, "Moegirl", "https://example.invalid/b"),
            new BirthdaySource("Kiana Kaslana", "HI3", 0, 0, "Baidu", "https://example.invalid/c", EvidenceExcerpt: "HTTP 403; UNKNOWN")
        ]);

        Assert.Equal((12, 7), (candidate.Month, candidate.Day));
        Assert.Equal(VerificationStatus.VerifiedMultiSource, candidate.VerificationStatus);
        Assert.Equal(3, candidate.Sources.Count);
        Assert.Contains("有日期的第三方来源", candidate.Evidence);
    }

    [Fact]
    public async Task FandomCharacterPages_ParseBirthdayWithoutPromotingToOfficial()
    {
        var handler = new Handler(new Dictionary<string, string>
        {
            ["categorymembers"] = "{\"query\":{\"categorymembers\":[{\"title\":\"Hotori\"}]}}",
            ["Hotori"] = "{\"query\":{\"pages\":{\"1\":{\"revisions\":[{\"slots\":{\"main\":{\"*\":\"|birthday = December 20\"}}}]}}}}"
        });
        var provider = new ThirdPartyBirthdayProvider(new HttpClient(handler));

        var candidates = await provider.CollectFandomCharactersAsync();

        var candidate = Assert.Single(candidates);
        Assert.Equal("Hotori", candidate.CanonicalCharacterNameZhCn);
        Assert.Equal((12, 20), (candidate.Month, candidate.Day));
        Assert.Equal(VerificationStatus.Unverified, candidate.VerificationStatus);
    }

    [Fact]
    public async Task NevernessGgPages_ParseExplicitBirthdateAndKeepTbaUnknown()
    {
        var handler = new Handler(new Dictionary<string, string>
        {
            ["https://neverness.gg/jiuyuan-nte-build/"] = "<html><body><p>Birthdate: July 24</p></body></html>",
            ["https://neverness.gg/shinku-nte-build/"] = "<html><body><p>Birthdate: TBA</p></body></html>"
        });
        var provider = new NteNevernessGgBirthdayProvider(new HttpClient(handler), [
            new NevernessCharacterPage("Jiuyuan", "https://neverness.gg/jiuyuan-nte-build/"),
            new NevernessCharacterPage("Shinku", "https://neverness.gg/shinku-nte-build/")
        ]);

        var candidates = await provider.CollectAsync();

        var jiuyuan = Assert.Single(candidates, x => x.Character == "Jiuyuan");
        Assert.Equal((7, 24), (jiuyuan.Month, jiuyuan.Day));
        var shinku = Assert.Single(candidates, x => x.Character == "Shinku");
        Assert.Equal((0, 0), (shinku.Month, shinku.Day));
    }

    private sealed class Handler(IReadOnlyDictionary<string, string> payloads) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var full = request.RequestUri?.ToString() ?? string.Empty;
            if (payloads.TryGetValue(full, out var direct))
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(direct) });
            var key = request.RequestUri?.Query.Contains("categorymembers", StringComparison.OrdinalIgnoreCase) == true
                ? "categorymembers"
                : Uri.UnescapeDataString(request.RequestUri?.Query.Split("titles=", StringSplitOptions.None).LastOrDefault()?.Split('&')[0] ?? string.Empty);
            payloads.TryGetValue(key, out var payload);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(payload ?? "{}") });
        }
    }
}
