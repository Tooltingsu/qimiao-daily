using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class GameRefreshOrchestratorTests
{
    [Fact]
    public async Task RefreshAllIncludesNteAndPreservesIndependentResults()
    {
        var jobs = new[]
        {
            new GameRefreshJob("GENSHIN", "原神", _ => Task.FromResult(GameCoverageResult.FromCounts("GENSHIN", 4, 4))),
            new GameRefreshJob("STARRAIL", "星铁", _ => Task.FromResult(GameCoverageResult.FromCounts("STARRAIL", 3, 3))),
            new GameRefreshJob("NTE", "异环", _ => Task.FromResult(GameCoverageResult.FromCounts("NTE", 2, 2)))
        };

        var report = await new GameRefreshOrchestrator(jobs).RefreshAllAsync();

        Assert.Equal(["GENSHIN", "STARRAIL", "NTE"], report.Games.Select(x => x.GameCode));
        Assert.All(report.Games, x => Assert.Equal("HEALTHY", x.HealthStatus));
    }

    [Fact]
    public void LowParserCoverageProducesWarningInsteadOfSuccess()
    {
        var result = GameCoverageResult.FromCounts("NTE", candidateCount: 20, parsedCount: 2, droppedCount: 18);

        Assert.Contains(result.Warnings, x => x.Contains("可能漏采", StringComparison.Ordinal));
        Assert.NotEqual("HEALTHY", result.HealthStatus);
        Assert.Equal(0.1, result.CoverageRatio, precision: 3);
    }

    [Fact]
    public void SourceCandidateCountCannotBeReportedAsParsedCount()
    {
        var result = GameCoverageResult.FromCounts("GENSHIN", candidateCount: 10, parsedCount: 7, droppedCount: 3);

        Assert.Equal(10, result.CandidateCount);
        Assert.Equal(7, result.ParsedCount);
        Assert.Equal(0.7, result.CoverageRatio, precision: 3);
        Assert.Equal("WARNING", result.HealthStatus);
    }

    [Fact]
    public void CoverageRetainsDroppedCountAndReasons()
    {
        var result = GameCoverageResult.FromCounts(
            "GENSHIN",
            candidateCount: 10,
            parsedCount: 7,
            droppedCount: 3,
            dropReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignored_rule"] = 2,
                ["missing_title"] = 1
            });

        Assert.Equal(3, result.DroppedCount);
        Assert.Equal(2, result.DropReasons["ignored_rule"]);
        Assert.Equal(1, result.DropReasons["missing_title"]);
        Assert.Contains(result.Warnings, warning => warning.Contains("规则过滤", StringComparison.Ordinal));
    }
}
