using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class OfficialVideoImportService(QimiaoDailyDbContext database)
{
    public async Task<bool> ImportAsync(GameCandidate candidate, CancellationToken cancellationToken = default)
    {
        if (candidate.ItemType is not ("VIDEO" or "PREVIEW_NOTICE" or "PREVIEW_LIVE")) throw new ArgumentException("Candidate is not a video or preview.");
        var classification = VideoPreviewClassifier.Classify(candidate.Title, candidate.Evidence.FirstOrDefault()?.SourceText);
        if (classification.Kind == VideoPreviewKind.Ignore) return false;
        var type = classification.Kind switch { VideoPreviewKind.PreviewNotice => "PREVIEW_NOTICE", VideoPreviewKind.PreviewLive => "PREVIEW_LIVE", _ => "VIDEO" };
        var normalizedCandidate = candidate with { ItemType = type };
        var identity = TimelineChangeClassifier.Identity(normalizedCandidate);
        var existing = await FindLatestAsync(identity, normalizedCandidate, cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.CanonicalIdentity)) existing.SetCanonicalIdentity(identity);
            if (TimelineChangeClassifier.Classify(existing, normalizedCandidate) == TimelineChangeKind.None) return false;
            database.TimelineItems.Add(CreateItem(normalizedCandidate, identity, TimelineChangeClassifier.Classify(existing, normalizedCandidate)));
        }
        else
        {
            database.TimelineItems.Add(CreateItem(normalizedCandidate, identity, TimelineChangeClassifier.InitialKind(normalizedCandidate)));
        }
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TimelineItem?> FindLatestAsync(string identity, GameCandidate candidate, CancellationToken cancellationToken)
    {
        var urls = candidate.Evidence.Select(x => x.SourceUrl).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var items = await database.TimelineItems.Include(x => x.Evidence)
            .Where(x => x.CanonicalIdentity == identity || (x.CanonicalIdentity == string.Empty && x.Evidence.Any(e => urls.Contains(e.SourceUrl))))
            .ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.FetchedAt).FirstOrDefault();
    }

    private static TimelineItem CreateItem(GameCandidate candidate, string identity, TimelineChangeKind change)
    {
        var item = new TimelineItem(candidate.GameCode, candidate.ItemType, candidate.Title, VerificationStatus.VerifiedOfficial,
            candidate.SourceTime, candidate.SourceTimezone, candidate.NormalizedTime, candidate.NormalizedTime is null ? TimePrecision.DateOnly : TimePrecision.Exact, DateTimeOffset.UtcNow);
        item.SetCanonicalIdentity(identity);
        item.SetChangeKind(change);
        foreach (var evidence in candidate.Evidence)
            item.AddEvidence(new EvidenceRecord(evidence.Provider, evidence.SourceType, evidence.SourceUrl, evidence.SourceText, "official-video-v1", evidence.FetchedAt, evidence.PageTitle, evidence.PublishedAt, evidence.OriginalTimezone, evidence.NormalizedTime, VerificationStatus.VerifiedOfficial));
        return item;
    }
}
