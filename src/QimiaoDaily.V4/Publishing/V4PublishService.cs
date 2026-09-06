using QimiaoDaily.V4.Core;
using System.Security.Cryptography;
using System.Text;

namespace QimiaoDaily.V4.Publishing;

public sealed class V4PublishService(V4Repository repository)
{
    public ReportRevision Lock(DateOnly date, bool manual, DateTimeOffset now)
    {
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        if (manifest.LockedRevision is not null)
            return repository.Read<ReportRevision>("reports", folder, "revisions", manifest.LockedRevision.Value.ToString("000") + ".json");
        var revision = repository.Read<ReportRevision>("reports", folder, "revisions", manifest.LatestRevision.ToString("000") + ".json");
        if (revision.State != ReportState.Ready) throw new InvalidOperationException("Only a READY revision can be locked.");
        Verify(revision);
        revision.State = manual ? ReportState.LockedManual : ReportState.LockedAuto;
        revision.LockedAt = now;
        revision.LockReason = manual ? "MANUAL_CONFIRMATION" : "AUTO_DEADLINE";
        manifest.LockedRevision = revision.Revision;
        manifest.State = revision.State;
        manifest.LockedAt = now;
        manifest.LockReason = revision.LockReason;
        repository.Write(revision, "reports", folder, "revisions", revision.Revision.ToString("000") + ".json");
        repository.Write(manifest, "reports", folder, "manifest.json");
        return revision;
    }

    // A user may explicitly replace an unpublished lock after reviewing a
    // newer revision. This is never implicit: the caller supplies both the
    // exact revision and an audit reason. A published lock cannot be replaced.
    public ReportRevision ReplaceUnpublishedLock(DateOnly date, int revisionNumber, DateTimeOffset now, string reason)
    {
        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A replacement-lock reason is required.", nameof(reason));
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        if (manifest.PublishedAt is not null)
            throw new InvalidOperationException("A published lock cannot be replaced; use republish instead.");
        if (manifest.LockedRevision == revisionNumber)
            return repository.Read<ReportRevision>("reports", folder, "revisions", revisionNumber.ToString("000") + ".json");

        var replacement = repository.Read<ReportRevision>("reports", folder, "revisions", revisionNumber.ToString("000") + ".json");
        if (replacement.State != ReportState.Ready)
            throw new InvalidOperationException("Replacement lock requires a READY revision.");
        Verify(replacement);

        if (manifest.LockedRevision is { } priorNumber)
        {
            var prior = repository.Read<ReportRevision>("reports", folder, "revisions", priorNumber.ToString("000") + ".json");
            if (prior.PublishedAt is not null)
                throw new InvalidOperationException("A published lock cannot be replaced; use republish instead.");
            prior.State = ReportState.Superseded;
            repository.Write(prior, "reports", folder, "revisions", priorNumber.ToString("000") + ".json");
        }

        replacement.State = ReportState.LockedManual;
        replacement.LockedAt = now;
        replacement.LockReason = "MANUAL_REPLACEMENT: " + reason.Trim();
        manifest.LockedRevision = replacement.Revision;
        manifest.State = replacement.State;
        manifest.SourceCommit = replacement.SourceCommit;
        manifest.ReportHash = replacement.ReportHash;
        manifest.GeneratedAt = replacement.GeneratedAt;
        manifest.LockedAt = replacement.LockedAt;
        manifest.LockReason = replacement.LockReason;
        repository.Write(replacement, "reports", folder, "revisions", replacement.Revision.ToString("000") + ".json");
        repository.Write(manifest, "reports", folder, "manifest.json");
        return replacement;
    }

    public PublishAttempt PublishDryRun(DateOnly date, string workflowRun, DateTimeOffset now, bool force = false, string? reason = null)
    {
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        ReportRevision revision;
        if (manifest.LockedRevision is { } locked)
        {
            revision = repository.Read<ReportRevision>("reports", folder, "revisions", locked.ToString("000") + ".json");
        }
        else
        {
            revision = Lock(date, false, now);
            manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        }
        var log = repository.ReadOr(new PublishLog { Date = date }, "publish-log", folder + ".json");
        if (!force && log.Attempts.Any(x => x.Status is "PUBLISHED" or "DRY_RUN_SUCCEEDED"))
            throw new InvalidOperationException("Idempotency guard: this date already has a successful publication attempt.");
        if (!force && log.Attempts.Any(x => x.ReportHash == revision.ReportHash && x.Status is "PUBLISHED" or "DRY_RUN_SUCCEEDED"))
            throw new InvalidOperationException("Idempotency guard: this date + reportHash was already published.");

        Verify(revision);
        var attempt = new PublishAttempt(revision.Revision, revision.ReportHash, revision.SourceCommit, null, null, now, null,
            workflowRun, "DRY_RUN_SUCCEEDED", null, true, reason);
        log.Attempts.Add(attempt);
        repository.Write(log, "publish-log", folder + ".json");
        manifest.State = ReportState.DryRunSucceeded;
        manifest.PublishedAt = null;
        repository.Write(manifest, "reports", folder, "manifest.json");
        return attempt;
    }

    public ReportRevision PrepareRepublication(DateOnly date, string sourceCommit, DateTimeOffset now, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A republication reason is required.", nameof(reason));
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        manifest.LockedRevision = null;
        manifest.LockedAt = null;
        manifest.LockReason = null;
        manifest.PublishedAt = null;
        manifest.State = ReportState.RepublicationReady;
        repository.Write(manifest, "reports", folder, "manifest.json");
        return new Generator.V4ReportGenerator(repository).Generate(date, sourceCommit, now);
    }

    private static void Verify(ReportRevision revision)
    {
        var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision.Content))).ToLowerInvariant();
        if (hash != revision.ReportHash) throw new InvalidDataException("Locked report hash mismatch.");
        if (revision.ValidationState != "VALID") throw new InvalidDataException("Revision is not VALID.");
        if (revision.PayloadHash is not null && revision.PayloadHash != Generator.V4ReportGenerator.PayloadHash(revision.Content, revision.SelectedArtwork))
            throw new InvalidDataException("Selected artwork payload hash mismatch.");
        foreach (var image in revision.SelectedArtwork)
            if (!image.SelectedForReport || !image.ReviewStatus.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Publish payload contains unselected/unconfirmed artwork.");
    }
}
