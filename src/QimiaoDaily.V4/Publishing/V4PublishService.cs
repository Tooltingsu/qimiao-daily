using QimiaoDaily.V4.Core;

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

        var attempt = new PublishAttempt(revision.Revision, revision.ReportHash, revision.SourceCommit, null, null, now, now,
            workflowRun, "DRY_RUN_SUCCEEDED", null, true, reason);
        log.Attempts.Add(attempt);
        repository.Write(log, "publish-log", folder + ".json");
        manifest.State = ReportState.Published;
        manifest.PublishedAt = now;
        revision.State = ReportState.Published;
        revision.PublishedAt = now;
        repository.Write(manifest, "reports", folder, "manifest.json");
        repository.Write(revision, "reports", folder, "revisions", revision.Revision.ToString("000") + ".json");
        return attempt;
    }

    public ReportRevision PrepareRepublication(DateOnly date, string sourceCommit, DateTimeOffset now, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A republication reason is required.", nameof(reason));
        var folder = date.ToString("yyyy-MM-dd");
        var manifest = repository.Read<ReportManifest>("reports", folder, "manifest.json");
        if (manifest.State == ReportState.Published)
        {
            var previous = repository.Read<ReportRevision>("reports", folder, "revisions", manifest.LockedRevision!.Value.ToString("000") + ".json");
            previous.State = ReportState.Superseded;
            repository.Write(previous, "reports", folder, "revisions", previous.Revision.ToString("000") + ".json");
        }
        manifest.LockedRevision = null;
        manifest.LockedAt = null;
        manifest.LockReason = null;
        manifest.PublishedAt = null;
        manifest.State = ReportState.RepublicationReady;
        repository.Write(manifest, "reports", folder, "manifest.json");
        return new Generator.V4ReportGenerator(repository).Generate(date, sourceCommit, now);
    }
}
