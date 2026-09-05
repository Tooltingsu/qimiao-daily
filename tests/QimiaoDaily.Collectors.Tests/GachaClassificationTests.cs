using QimiaoDaily.Collectors;

namespace QimiaoDaily.Collectors.Tests;

public sealed class GachaClassificationTests
{
    [Fact]
    public void GenshinChronicledUpperHalf_UsesExplicitEvidence()
    {
        var result = GachaClassification.Classify("GENSHIN", "\u96c6\u5f55\u7948\u613f\u00b7\u4e0a\u534a", "\u672c\u671f\u96c6\u5f55\u7948\u613f\uff0c\u4e0a\u534a\u5f00\u542f\u3002");
        Assert.Equal("CHRONICLED", result.PoolKind);
        Assert.Equal("FIRST_HALF", result.PoolPhase);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void StarRailLightConeSecondHalf_UsesExplicitEvidence()
    {
        var result = GachaClassification.Classify("STARRAIL", "\u5149\u9525\u6d3b\u52a8\u8dc3\u8fc1\u00b7\u4e0b\u534a", "\u5149\u9525\u6d3b\u52a8\u8dc3\u8fc1\u4e0b\u534a\u5f00\u653e\u3002");
        Assert.Equal("LIGHT_CONE", result.PoolKind);
        Assert.Equal("SECOND_HALF", result.PoolPhase);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void MissingEvidence_RemainsUnknown()
    {
        var result = GachaClassification.Classify("GENSHIN", "\u7948\u613f\u516c\u544a", "\u7248\u672c\u6d3b\u52a8\u8bf4\u660e\u3002");
        Assert.Equal("UNKNOWN", result.PoolKind);
        Assert.Equal("UNKNOWN", result.PoolPhase);
        Assert.Null(result.GroupKey);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void ContradictoryPhaseEvidence_IsMarkedConflict()
    {
        var result = GachaClassification.Classify("GENSHIN", "\u7948\u613f\u00b7\u4e0a\u534a", "\u6b63\u6587\u540c\u65f6\u5199\u660e\u4e0b\u534a\u5f00\u542f\u3002");
        Assert.True(result.HasConflict);
        Assert.Equal("UNKNOWN", result.PoolPhase);
    }
}
