using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class SourceSettingsTests
{
    [Fact]
    public void Load_UsesConfiguredRepositoriesAndArtworkStrategy()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-source-settings-" + Guid.NewGuid().ToString("N"));
        var paths = new QimiaoDailyPaths(root);
        paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(paths.ConfigDirectory, SourceSettings.FileName), """
        {
          "bgiRepositories": ["owner/primary", "owner/secondary", "owner/primary"],
          "artwork": { "dailyRankingLimit": 12, "targetCount": 8, "directArtworkIds": ["100", "100", "invalid"] }
        }
        """);

        var settings = SourceSettings.Load(paths);

        Assert.Equal(["owner/primary", "owner/secondary"], settings.BgiRepositories);
        Assert.Equal(12, settings.ArtworkDailyRankingLimit);
        Assert.Equal(8, settings.ArtworkTargetCount);
        Assert.Equal(["100"], settings.ArtworkIds);
    }

    [Fact]
    public void Load_InvalidFileFallsBackToDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-source-settings-" + Guid.NewGuid().ToString("N"));
        var paths = new QimiaoDailyPaths(root);
        paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(paths.ConfigDirectory, SourceSettings.FileName), "not-json");

        Assert.Equal(SourceSettings.Default, SourceSettings.Load(paths));
    }
}
