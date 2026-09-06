using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Publishing;

// Mutates the confirmed-artwork queue only after a genuine production result.
// Test runs, dry runs, generation and locking intentionally cannot call this
// path, so a queued image is never lost merely because it was previewed.
public sealed class ArtworkQueueService(V4Repository repository)
{
    public int ConsumeAfterProductionPublication(ReportRevision revision, PublishAttempt attempt)
    {
        if (attempt.DryRun || !string.Equals(attempt.Status, "PUBLISHED", StringComparison.Ordinal))
            throw new InvalidOperationException("Artwork queue may only advance after a real PUBLISHED attempt.");
        if (!string.Equals(attempt.ReportHash, revision.ReportHash, StringComparison.Ordinal))
            throw new InvalidDataException("Artwork queue consumption requires the published revision hash.");

        var sent = revision.SelectedArtwork
            .Select(x => ArtworkKey(x.Platform, x.ArtworkId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sent.Count == 0) return 0;

        var queue = repository.Read<List<ArtworkQueueEntry>>("data", "artwork-queue.json");
        var remaining = queue.Where(item => !sent.Contains(ArtworkKey(item.Platform, item.ArtworkId))).ToList();
        var removed = queue.Count - remaining.Count;
        if (removed != sent.Count)
            throw new InvalidDataException("Artwork queue changed after the report was locked; refusing to consume a different item.");

        // Delete from the confirmed area, not from collector metadata. The
        // immutable report revision remains the historical publication record.
        repository.Write(remaining, "data", "artwork-queue.json");
        return removed;
    }

    private static string ArtworkKey(string platform, string artworkId) => platform.Trim() + "\u001f" + artworkId.Trim();
}
