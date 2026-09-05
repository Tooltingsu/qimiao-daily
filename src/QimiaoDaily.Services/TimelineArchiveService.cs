using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class TimelineArchiveService(QimiaoDailyDbContext database)
{
    public async Task<int> ArchiveExpiredAsync(DateTimeOffset now, string actor = "system", CancellationToken cancellationToken = default)
    {
        var cutoff = now.AddDays(-3);
        var candidates = await database.TimelineItems.Where(x => x.ReviewStatus != ReviewStatus.Archived && x.EndAt != null).ToListAsync(cancellationToken);
        var expired = candidates.Where(x => x.EndAt!.Value < cutoff).ToList();
        foreach (var item in expired)
        {
            var oldStatus = item.ReviewStatus.ToString();
            item.Archive(actor, "end_at + 3 days elapsed");
            database.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), item.Id, "ARCHIVE", actor, "end_at + 3 days elapsed", now));
            database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "ReviewStatus", oldStatus, item.ReviewStatus.ToString(), actor, "end_at + 3 days elapsed", now));
        }
        await database.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
