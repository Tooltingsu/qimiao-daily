using QimiaoDaily.Data;
using System.Runtime.Versioning;

namespace QimiaoDaily.IntegrationTests;

[SupportedOSPlatform("windows")]
public sealed class SecureSettingsStoreTests
{
    [Fact]
    public void SetAndGet_UsesEncryptedFileAndRoundTripsCurrentUserSecret()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-secret-" + Guid.NewGuid().ToString("N"));
        var paths = new QimiaoDailyPaths(root);
        var store = new SecureSettingsStore(paths);

        store.Set("pixiv_session", "SEKRET_COOKIE_VALUE");

        Assert.True(store.Has("pixiv_session"));
        Assert.Equal("SEKRET_COOKIE_VALUE", store.TryGet("pixiv_session"));
        var file = Path.Combine(paths.ConfigDirectory, "pixiv_session.dpapi");
        Assert.NotEqual("SEKRET_COOKIE_VALUE", File.ReadAllText(file));
        store.Delete("pixiv_session");
        Assert.False(store.Has("pixiv_session"));
    }
}
