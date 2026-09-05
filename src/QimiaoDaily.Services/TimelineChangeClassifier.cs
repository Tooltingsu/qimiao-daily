using QimiaoDaily.Collectors;
using QimiaoDaily.Core;

namespace QimiaoDaily.Services;

public static class TimelineChangeClassifier
{
    public static string Identity(GameCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.GameCode) || string.IsNullOrWhiteSpace(candidate.ExternalId)) throw new ArgumentException("Game candidate identity is required.");
        return $"{candidate.GameCode.Trim().ToUpperInvariant()}:{candidate.ExternalId.Trim().ToLowerInvariant()}";
    }

    public static TimelineChangeKind Classify(TimelineItem previous, GameCandidate candidate)
    {
        if (previous.VerificationStatus == VerificationStatus.Conflict || HasConflictingEvidence(previous) || HasConflictingEvidence(candidate)) return TimelineChangeKind.Conflict;
        if (previous.NormalizedTime != candidate.NormalizedTime || previous.EndAt != candidate.EndAt) return TimelineChangeKind.TimeChanged;
        if (!string.Equals(previous.GachaPoolKind, candidate.GachaPoolKind, StringComparison.Ordinal) || !string.Equals(previous.GachaPoolPhase, candidate.GachaPoolPhase, StringComparison.Ordinal) || !string.Equals(previous.GachaGroupKey, candidate.GachaGroupKey, StringComparison.Ordinal)) return TimelineChangeKind.ContentChanged;
        if (!string.Equals(previous.Title, candidate.Title.Trim(), StringComparison.Ordinal) || !string.Equals(previous.ItemType, candidate.ItemType.Trim(), StringComparison.Ordinal) || EvidenceText(previous) != EvidenceText(candidate)) return TimelineChangeKind.ContentChanged;
        if (!EvidenceUrls(previous).SetEquals(candidate.Evidence.Select(x => x.SourceUrl).Where(x => !string.IsNullOrWhiteSpace(x)))) return TimelineChangeKind.SourceChanged;
        return TimelineChangeKind.None;
    }

    public static TimelineChangeKind InitialKind(GameCandidate candidate) => HasConflictingEvidence(candidate) ? TimelineChangeKind.Conflict : TimelineChangeKind.New;

    private static bool HasConflictingEvidence(TimelineItem item)
    {
        var times = item.Evidence.Select(x => x.NormalizedTime).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        return times.Count > 1;
    }

    private static bool HasConflictingEvidence(GameCandidate candidate)
    {
        var times = candidate.Evidence.Select(x => x.NormalizedTime).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        return times.Count > 1;
    }

    private static HashSet<string> EvidenceUrls(TimelineItem item) => item.Evidence.Select(x => x.SourceUrl).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string EvidenceText(TimelineItem item) => string.Join("\n", item.Evidence.OrderBy(x => x.SourceUrl, StringComparer.OrdinalIgnoreCase).Select(x => x.SourceText.Trim()));

    private static string EvidenceText(GameCandidate candidate) => string.Join("\n", candidate.Evidence.OrderBy(x => x.SourceUrl, StringComparer.OrdinalIgnoreCase).Select(x => x.SourceText.Trim()));
}
