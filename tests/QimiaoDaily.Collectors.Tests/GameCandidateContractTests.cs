using QimiaoDaily.Collectors;
using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors.Tests;

public sealed class GameCandidateContractTests
{
    [Fact]
    public void RelativeCandidateRetainsExpressionAndEvidenceKeys()
    {
        var candidate = GameCandidate.Relative(
            "star-1", "STARRAIL", "GACHA", "活动跃迁",
            "2026/07/15 4.4版本更新后 - 2026/08/25 15:00", "Asia/Shanghai",
            "4.4版本更新后", "2026/08/25 15:00", "announcement-content-1");

        Assert.Null(candidate.NormalizedTime);
        Assert.Equal(TimePrecision.Relative, candidate.StartTimePrecision);
        Assert.Equal("announcement-content-1", candidate.StartTimeEvidenceKey);
        Assert.Equal("4.4版本更新后", candidate.StartExpression);
    }
}
