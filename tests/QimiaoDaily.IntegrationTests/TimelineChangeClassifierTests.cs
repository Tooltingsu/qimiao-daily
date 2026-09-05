using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class TimelineChangeClassifierTests
{
    [Fact]
    public void Classify_DistinguishesNoChangeTimeContentSourceAndConflict()
    {
        var start = new DateTimeOffset(2026, 8, 20, 2, 0, 0, TimeSpan.Zero);
        var previous = Item("title", start, "https://example.invalid/a", "body");
        var same = Candidate("title", start, "https://example.invalid/a", "body");
        Assert.Equal(TimelineChangeKind.None, TimelineChangeClassifier.Classify(previous, same));
        Assert.Equal(TimelineChangeKind.TimeChanged, TimelineChangeClassifier.Classify(previous, Candidate("title", start.AddHours(1), "https://example.invalid/a", "body")));
        Assert.Equal(TimelineChangeKind.ContentChanged, TimelineChangeClassifier.Classify(previous, Candidate("new title", start, "https://example.invalid/a", "body")));
        Assert.Equal(TimelineChangeKind.SourceChanged, TimelineChangeClassifier.Classify(previous, Candidate("title", start, "https://example.invalid/b", "body")));

        var conflict = Candidate("title", start, "https://example.invalid/a", "body") with
        {
            Evidence = [
                new CollectedEvidence("a", "notice", "https://example.invalid/a", "body", DateTimeOffset.UtcNow, NormalizedTime: start),
                new CollectedEvidence("b", "notice", "https://example.invalid/b", "body", DateTimeOffset.UtcNow, NormalizedTime: start.AddHours(1))]
        };
        Assert.Equal(TimelineChangeKind.Conflict, TimelineChangeClassifier.Classify(previous, conflict));
        Assert.Equal("GENSHIN:external-1", TimelineChangeClassifier.Identity(same));
    }

    private static TimelineItem Item(string title, DateTimeOffset start, string url, string body)
    {
        var item = new TimelineItem("GENSHIN", "EVENT", title, VerificationStatus.VerifiedOfficial, start.ToString("O"), "UTC", start, TimePrecision.Exact, DateTimeOffset.UtcNow);
        item.AddEvidence(new EvidenceRecord("official", "notice", url, body, "test", DateTimeOffset.UtcNow, normalizedTime: start));
        item.SetCanonicalIdentity("GENSHIN:external-1");
        return item;
    }

    private static GameCandidate Candidate(string title, DateTimeOffset start, string url, string body) => new(
        "external-1", "GENSHIN", "EVENT", title, start.ToString("O"), "UTC", start,
        [new CollectedEvidence("official", "notice", url, body, DateTimeOffset.UtcNow, NormalizedTime: start)]);
}
