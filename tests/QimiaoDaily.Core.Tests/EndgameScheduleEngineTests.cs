using QimiaoDaily.Services;

namespace QimiaoDaily.Core.Tests;

public sealed class EndgameScheduleEngineTests
{
    [Fact]
    public void BuildCurrentAndNextTwo_UsesMonthlyGenshinWindowsAtShanghaiFour()
    {
        var result = new EndgameScheduleEngine().BuildCurrentAndNextTwo(
            EndgameScheduleRules.GenshinSpiralAbyss,
            new DateOnly(2026, 8, 20));

        Assert.Equal([new DateOnly(2026, 8, 16), new DateOnly(2026, 9, 16), new DateOnly(2026, 10, 16)], result.Select(x => x.StartsOn));
        Assert.Equal([new DateOnly(2026, 9, 16), new DateOnly(2026, 10, 16), new DateOnly(2026, 11, 16)], result.Select(x => x.EndsOn));
        Assert.All(result, x => Assert.Equal(new TimeOnly(4, 0), x.StartTime));
        Assert.All(result, x => Assert.Equal(new TimeOnly(4, 0), x.EndTime));
    }

    [Fact]
    public void BuildCurrentAndNextTwo_RecalculatesOnlyDependentRulesFromVersionWindows()
    {
        var versions = new[]
        {
            new VersionWindow("GENSHIN", "6.8", new DateOnly(2026, 8, 20), new DateOnly(2026, 10, 1)),
            new VersionWindow("GENSHIN", "7.0", new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12)),
            new VersionWindow("GENSHIN", "7.1", new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 24)),
        };
        var engine = new EndgameScheduleEngine();

        var stygian = engine.BuildCurrentAndNextTwo(EndgameScheduleRules.GenshinStygianOnslaught, new DateOnly(2026, 8, 25), versions);
        var arbitration = engine.BuildCurrentAndNextTwo(EndgameScheduleRules.StarRailSectorArbitration, new DateOnly(2026, 8, 25),
            [new VersionWindow("STARRAIL", "3.6", new DateOnly(2026, 8, 13), new DateOnly(2026, 9, 24)),
             new VersionWindow("STARRAIL", "3.7", new DateOnly(2026, 9, 24), new DateOnly(2026, 11, 5)),
             new VersionWindow("STARRAIL", "3.8", new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 17))]);

        Assert.Equal([new DateOnly(2026, 8, 27), new DateOnly(2026, 10, 8), new DateOnly(2026, 11, 19)], stygian.Select(x => x.StartsOn));
        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 24)], stygian.Select(x => x.EndsOn));
        Assert.Equal(["6.8", "7.0", "7.1"], stygian.Select(x => x.VersionNumber));
        Assert.Equal([new DateOnly(2026, 8, 13), new DateOnly(2026, 9, 24), new DateOnly(2026, 11, 5)], arbitration.Select(x => x.StartsOn));
        Assert.Equal(EndgameScheduleRules.StarRailSectorArbitration.RuleId, arbitration[0].RuleId);
    }

    [Fact]
    public void BuildCurrentAndNextTwo_UsesVersionWindowForFrenziedAndDoesNotMixGames()
    {
        var result = new EndgameScheduleEngine().BuildCurrentAndNextTwo(
            EndgameScheduleRules.GenshinFrenziedOnslaught,
            new DateOnly(2026, 8, 25),
            [new VersionWindow("STARRAIL", "3.6", new DateOnly(2026, 8, 13), new DateOnly(2026, 9, 24)),
             new VersionWindow("GENSHIN", "6.8", new DateOnly(2026, 8, 20), new DateOnly(2026, 10, 1)),
             new VersionWindow("GENSHIN", "7.0", new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12)),
             new VersionWindow("GENSHIN", "7.1", new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 24))]);

        Assert.Equal([new DateOnly(2026, 8, 27), new DateOnly(2026, 10, 8), new DateOnly(2026, 11, 19)], result.Select(x => x.StartsOn));
        Assert.Equal([new DateOnly(2026, 9, 6), new DateOnly(2026, 10, 18), new DateOnly(2026, 11, 29)], result.Select(x => x.EndsOn));
        Assert.All(result, x => Assert.Equal(new TimeOnly(4, 0), x.StartTime));
        Assert.All(result, x => Assert.Equal(new TimeOnly(4, 0), x.EndTime));
    }

    [Fact]
    public void BuildCurrentAndNextTwo_UsesDateOnlyFourteenDayFridayCycleForOuterRealm()
    {
        var rule = EndgameScheduleRules.OuterRealm;

        var result = new EndgameScheduleEngine().BuildCurrentAndNextTwo(rule, new DateOnly(2026, 8, 25));

        Assert.Equal([new DateOnly(2026, 8, 21), new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 18)], result.Select(x => x.StartsOn));
        Assert.All(result, x => Assert.Equal(EndgameTimePrecision.DateOnly, x.Precision));
        Assert.All(result, x => Assert.Equal(DayOfWeek.Friday, x.StartsOn.DayOfWeek));
    }

    [Fact]
    public void BuildCurrentAndNextTwo_UsesStarRailFortyTwoDayRule()
    {
        var result = new EndgameScheduleEngine().BuildCurrentAndNextTwo(
            EndgameScheduleRules.StarRailMemoryOfChaos,
            new DateOnly(2026, 9, 1));

        Assert.Equal([new DateOnly(2026, 8, 17), new DateOnly(2026, 9, 28), new DateOnly(2026, 11, 9)], result.Select(x => x.StartsOn));
        Assert.All(result, x => Assert.Equal(new TimeOnly(4, 0), x.StartTime));
    }

    [Fact]
    public void ReanchorAndOverride_AffectOnlyTheRequestedRule()
    {
        var engine = new EndgameScheduleEngine();
        var reanchored = engine.Reanchor(EndgameScheduleRules.OuterRealm, new DateOnly(2026, 8, 28));
        var overridden = engine.WithOverride(reanchored, new EndgameOccurrenceOverride(new DateOnly(2026, 9, 11), Notes: "maintenance"));

        var changed = engine.BuildCurrentAndNextTwo(overridden, new DateOnly(2026, 8, 29));
        var unrelated = engine.BuildCurrentAndNextTwo(EndgameScheduleRules.StarRailMemoryOfChaos, new DateOnly(2026, 8, 29));

        Assert.Equal(new DateOnly(2026, 9, 11), changed[1].StartsOn);
        Assert.Equal("maintenance", changed[1].Notes);
        Assert.Equal(new DateOnly(2026, 8, 17), unrelated[0].StartsOn);
    }
}
