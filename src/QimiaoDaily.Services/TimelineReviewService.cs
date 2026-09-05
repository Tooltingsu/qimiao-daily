using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class TimelineReviewService(QimiaoDailyDbContext database)
{
    public async Task EditAsync(Guid itemId, string itemType, string title, VerificationStatus verificationStatus,
        string? sourceTime, string? sourceTimezone, DateTimeOffset? normalizedTime, TimePrecision timePrecision,
        DateTimeOffset fetchedAt, DateTimeOffset? endAt, string actor, string reason, DateTimeOffset now,
        TimePrecision? startTimePrecision = null, TimePrecision? endTimePrecision = null,
        string? startTimeSource = null, string? endTimeSource = null, string? startExpression = null, string? endExpression = null,
        string? startTimeEvidenceKey = null, string? endTimeEvidenceKey = null,
        string? gachaPoolKind = null, string? gachaPoolPhase = null, string? gachaGroupKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("编辑需要操作者和原因。");
        var item = await database.TimelineItems.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Timeline item was not found.");
        var changes = item.Edit(itemType, title, verificationStatus, sourceTime, sourceTimezone, normalizedTime, timePrecision, fetchedAt, endAt,
            startTimePrecision, endTimePrecision, startTimeSource, endTimeSource, startExpression, endExpression, startTimeEvidenceKey, endTimeEvidenceKey,
            gachaPoolKind, gachaPoolPhase, gachaGroupKey);
        database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "EDIT", actor, reason, now));
        foreach (var change in changes)
            database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, change.FieldName, change.OldValue, change.NewValue, actor, reason, now));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmAsync(Guid itemId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("未找到候选项。");
        var oldStatus = item.ReviewStatus.ToString();
        item.Confirm(actor, reason, now);
        database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "CONFIRM", actor, reason, now));
        database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "ReviewStatus", oldStatus, item.ReviewStatus.ToString(), actor, reason, now));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ReturnAsync(Guid itemId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Timeline item was not found.");
        var oldStatus = item.ReviewStatus.ToString();
        item.ReturnToReview(actor, reason);
        database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "RETURN", actor, reason, now));
        database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "ReviewStatus", oldStatus, item.ReviewStatus.ToString(), actor, reason, now));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid itemId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Timeline item was not found.");
        var oldStatus = item.ReviewStatus.ToString();
        item.Archive(actor, reason);
        database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "ARCHIVE", actor, reason, now));
        database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "ReviewStatus", oldStatus, item.ReviewStatus.ToString(), actor, reason, now));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid itemId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Timeline item was not found.");
        var oldStatus = item.ReviewStatus.ToString();
        item.RestoreFromArchive(actor, reason);
        database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "RESTORE", actor, reason, now));
        database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "ReviewStatus", oldStatus, item.ReviewStatus.ToString(), actor, reason, now));
        await database.SaveChangesAsync(cancellationToken);
    }
}
