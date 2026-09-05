using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed record TimelineImportResult(Guid ItemId, TimelineChangeKind ChangeKind);

public sealed class TimelineCandidateImportService(QimiaoDailyDbContext database)
{
    public async Task<TimelineImportResult> ApplyCandidateAsync(
        GameCandidate candidate,
        string parserVersion,
        string actor,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parserVersion)) throw new ArgumentException("Parser version is required.", nameof(parserVersion));
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Revision actor and reason are required.");

        var identity = TimelineChangeClassifier.Identity(candidate);
        var existing = await FindLatestAsync(identity, candidate, cancellationToken);
        if (existing is null)
        {
            var item = CreateItem(candidate, identity, TimelineChangeClassifier.InitialKind(candidate), parserVersion);
            database.TimelineItems.Add(item);
            await database.SaveChangesAsync(cancellationToken);
            return new(item.Id, item.ChangeKind);
        }

        if (string.IsNullOrWhiteSpace(existing.CanonicalIdentity)) existing.SetCanonicalIdentity(identity);
        var change = TimelineChangeClassifier.Classify(existing, candidate);
        if (change == TimelineChangeKind.None)
        {
            await database.SaveChangesAsync(cancellationToken);
            return new(existing.Id, TimelineChangeKind.None);
        }

        var next = CreateItem(candidate, identity, change, parserVersion);
        var now = DateTimeOffset.UtcNow;
        AddRevision(existing, next, "NormalizedTime", actor, reason, now);
        AddRevision(existing, next, "EndAt", actor, reason, now);
        AddRevision(existing, next, "TimePrecision", actor, reason, now);
        AddRevision(existing, next, "StartTimePrecision", actor, reason, now);
        AddRevision(existing, next, "EndTimePrecision", actor, reason, now);
        AddRevision(existing, next, "StartTimeSource", actor, reason, now);
        AddRevision(existing, next, "EndTimeSource", actor, reason, now);
        AddRevision(existing, next, "StartExpression", actor, reason, now);
        AddRevision(existing, next, "EndExpression", actor, reason, now);
        AddRevision(existing, next, "StartTimeEvidenceKey", actor, reason, now);
        AddRevision(existing, next, "EndTimeEvidenceKey", actor, reason, now);
        AddRevision(existing, next, "GachaPoolKind", actor, reason, now);
        AddRevision(existing, next, "GachaPoolPhase", actor, reason, now);
        AddRevision(existing, next, "GachaGroupKey", actor, reason, now);
        if (existing.ReviewStatus != ReviewStatus.Pending)
            database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), next.Id, "ReviewStatus", existing.ReviewStatus.ToString(), next.ReviewStatus.ToString(), actor, reason, now));
        database.TimelineItems.Add(next);
        await database.SaveChangesAsync(cancellationToken);
        return new(next.Id, change);
    }

    private async Task<TimelineItem?> FindLatestAsync(string identity, GameCandidate candidate, CancellationToken cancellationToken)
    {
        var urls = candidate.Evidence.Select(x => x.SourceUrl).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var items = await database.TimelineItems.Include(x => x.Evidence)
            .Where(x => x.CanonicalIdentity == identity || (x.CanonicalIdentity == string.Empty && x.Evidence.Any(e => urls.Contains(e.SourceUrl))))
            .ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.FetchedAt).FirstOrDefault();
    }

    private static TimelineItem CreateItem(GameCandidate candidate, string identity, TimelineChangeKind change, string parserVersion)
    {
        var hasExactStart = candidate.NormalizedTime is not null;
        var item = new TimelineItem(candidate.GameCode, candidate.ItemType, candidate.Title,
            hasExactStart ? VerificationStatus.VerifiedOfficial : VerificationStatus.Unverified,
            candidate.SourceTime, candidate.SourceTimezone, candidate.NormalizedTime, candidate.StartTimePrecision,
            DateTimeOffset.UtcNow, candidate.EndAt, candidate.StartTimePrecision, candidate.EndTimePrecision,
            candidate.StartTimeSource, candidate.EndTimeSource, candidate.StartExpression, candidate.EndExpression,
            candidate.StartTimeEvidenceKey, candidate.EndTimeEvidenceKey, candidate.GachaPoolKind, candidate.GachaPoolPhase, candidate.GachaGroupKey);
        item.SetCanonicalIdentity(identity);
        item.SetChangeKind(change);
        foreach (var evidence in candidate.Evidence)
            item.AddEvidence(new EvidenceRecord(evidence.Provider, evidence.SourceType, evidence.SourceUrl, evidence.SourceText,
                parserVersion, evidence.FetchedAt, evidence.PageTitle, evidence.PublishedAt, evidence.OriginalTimezone,
                evidence.NormalizedTime, hasExactStart ? VerificationStatus.VerifiedOfficial : VerificationStatus.Unverified));
        return item;
    }

    private void AddRevision(TimelineItem previous, TimelineItem next, string fieldName, string actor, string reason, DateTimeOffset now)
    {
        var oldValue = FieldValue(previous, fieldName);
        var newValue = FieldValue(next, fieldName);
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;
        database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), next.Id, fieldName, oldValue, newValue, actor, reason, now));
    }

    private static string FieldValue(TimelineItem item, string fieldName) => fieldName switch
    {
        "NormalizedTime" => item.NormalizedTime?.ToString("O") ?? string.Empty,
        "EndAt" => item.EndAt?.ToString("O") ?? string.Empty,
        "TimePrecision" => item.TimePrecision.ToString(),
        "StartTimePrecision" => item.StartTimePrecision.ToString(),
        "EndTimePrecision" => item.EndTimePrecision.ToString(),
        "StartTimeSource" => item.StartTimeSource ?? string.Empty,
        "EndTimeSource" => item.EndTimeSource ?? string.Empty,
        "StartExpression" => item.StartExpression ?? string.Empty,
        "EndExpression" => item.EndExpression ?? string.Empty,
        "StartTimeEvidenceKey" => item.StartTimeEvidenceKey ?? string.Empty,
        "EndTimeEvidenceKey" => item.EndTimeEvidenceKey ?? string.Empty,
        "GachaPoolKind" => item.GachaPoolKind ?? string.Empty,
        "GachaPoolPhase" => item.GachaPoolPhase ?? string.Empty,
        "GachaGroupKey" => item.GachaGroupKey ?? string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unsupported timeline revision field.")
    };
}
