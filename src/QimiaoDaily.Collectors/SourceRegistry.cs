namespace QimiaoDaily.Collectors;

public sealed record SourceDescriptor(string Key, string DisplayName, string GameCode, string ProviderType, IReadOnlyList<string> EndpointUrls);

/// Central source inventory used by health, scheduling and diagnostics. Provider code remains responsible for parsing.
public sealed class SourceRegistry
{
    private readonly Dictionary<string, SourceDescriptor> _sources = new(StringComparer.OrdinalIgnoreCase);

    public SourceRegistry(IEnumerable<SourceDescriptor>? sources = null)
    {
        foreach (var source in sources ?? DefaultSources())
        {
            _sources.Add(source.Key, source);
        }
    }

    public IReadOnlyCollection<SourceDescriptor> Sources => _sources.Values;

    public bool TryGet(string key, out SourceDescriptor descriptor) => _sources.TryGetValue(key, out descriptor!);

    public static SourceRegistry CreateDefault() => new();

    private static IEnumerable<SourceDescriptor> DefaultSources()
    {
        yield return new("genshin-official", "原神官方公告", "GENSHIN", nameof(GenshinAnnouncementProvider), [GenshinAnnouncementProvider.ListUrl, GenshinAnnouncementProvider.ContentUrl]);
        yield return new("starrail-official", "星铁官方公告", "STARRAIL", nameof(StarRailAnnouncementProvider), [StarRailAnnouncementProvider.ListUrl, StarRailAnnouncementProvider.ContentUrl]);
        yield return new("nte-bilibili-official", "异环官方 Bilibili", "NTE", nameof(NteBilibiliOfficialProvider), [NteBilibiliOfficialProvider.FeedUrl]);
        yield return new("nte-official-website", "异环官网公告与视频", "NTE", nameof(NteOfficialWebsiteProvider), [NteOfficialWebsiteProvider.NewsDataUrl, NteOfficialWebsiteProvider.MainPageUrl]);
        yield return new("pixiv-character-search", "Pixiv 日榜", "ARTWORK", nameof(PixivArtworkProvider), [PixivArtworkProvider.SearchUrl]);
        yield return new("nte-official-roster", "异环官网角色名册", "NTE", nameof(NteOfficialRosterProvider), [NteOfficialRosterProvider.MainPageUrl]);
        yield return new("ntegame-birthday", "NTEGame 生日资料（待审核）", "NTE", nameof(NteGameBirthdayProvider), [NteGameBirthdayProvider.ListUrl]);
        yield return new("github-bgi", "BGI GitHub（由 source_settings.json 配置）", "BGI", nameof(GitHubCommitProvider), []);
    }
}
