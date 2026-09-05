using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class SourceRegistryTests
{
    [Fact]
    public void DefaultRegistry_ContainsIndependentOfficialSources()
    {
        var registry = SourceRegistry.CreateDefault();
        Assert.Contains(registry.Sources, x => x.Key == "genshin-official" && x.ProviderType == nameof(GenshinAnnouncementProvider));
        Assert.Contains(registry.Sources, x => x.Key == "starrail-official" && x.ProviderType == nameof(StarRailAnnouncementProvider));
        Assert.Contains(registry.Sources, x => x.Key == "nte-bilibili-official" && x.ProviderType == nameof(NteBilibiliOfficialProvider));
        Assert.Contains(registry.Sources, x => x.Key == "pixiv-character-search" && x.ProviderType == nameof(PixivArtworkProvider));
    }
}
