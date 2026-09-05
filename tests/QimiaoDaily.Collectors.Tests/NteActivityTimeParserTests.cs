using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors.Tests;

public sealed class NteActivityTimeParserTests
{
    [Fact]
    public void NteNewsPublicationIsNotActivityStart()
    {
        var parsed = NteActivityTimeParser.ParseForTest(
            "活动时间：8月20日10:00-9月3日05:59",
            "8月13日 09:00");

        Assert.Equal(20, parsed.Start!.Value.Day);
        Assert.Equal(10, parsed.Start.Value.Hour);
        Assert.NotEqual(parsed.Start, parsed.PublishedAt);
        Assert.Equal(TimePrecision.Exact, parsed.Precision);
    }

    [Fact]
    public void NteRelativeVersionTimeStaysUnresolved()
    {
        var parsed = NteActivityTimeParser.ParseForTest(
            "活动时间：8月13日版本更新后-9月3日05:59",
            "8月13日 09:00");

        Assert.Null(parsed.Start);
        Assert.Equal(TimePrecision.Relative, parsed.Precision);
        Assert.Equal("8月13日版本更新后", parsed.StartExpression);
        Assert.NotNull(parsed.PublishedAt);
    }
}
