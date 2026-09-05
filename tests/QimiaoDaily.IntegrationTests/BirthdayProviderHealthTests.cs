using QimiaoDaily.Collectors;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class BirthdayProviderHealthTests
{
    [Fact]
    public void Summarize_ReportsPartialWhenSomeCandidatesHaveNoDate()
    {
        var result = BirthdayProviderHealth.Summarize(
        [
            new BirthdaySource("A", "HI3", 12, 7, "provider", "https://example.invalid/a"),
            new BirthdaySource("B", "HI3", 0, 0, "provider", "https://example.invalid/b")
        ]);

        Assert.Equal("PARTIAL", result.Status);
        Assert.Equal(1, result.Known);
        Assert.Equal(1, result.Unknown);
        Assert.Contains("1/2", result.Message);
    }

    [Fact]
    public void Summarize_ReportsFailedWhenProviderReturnsOnlyUnknownCandidates()
    {
        var result = BirthdayProviderHealth.Summarize(
        [new BirthdaySource("A", "HI3", 0, 0, "provider", "https://example.invalid/a")]);

        Assert.Equal("FAILED", result.Status);
        Assert.Equal(0, result.Known);
        Assert.Equal(1, result.Unknown);
    }
}
