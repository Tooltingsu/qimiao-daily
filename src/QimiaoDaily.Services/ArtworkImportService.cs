using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class ArtworkImportService(QimiaoDailyDbContext database)
{
    public async Task<bool> ImportAsync(OfficialArtworkCandidate candidate, string? contentSha256 = null, string? perceptualHash = null, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(candidate.SourceUrl);
        if (await database.SeenArtworks.AnyAsync(x => (x.Platform == candidate.Platform && x.ArtworkId == candidate.ArtworkId) || x.NormalizedUrl == normalized || (!string.IsNullOrWhiteSpace(contentSha256) && x.ContentSha256 == contentSha256) || (!string.IsNullOrWhiteSpace(perceptualHash) && x.PerceptualHash == perceptualHash), cancellationToken)) return false;
        if (!string.IsNullOrWhiteSpace(perceptualHash))
        {
            var knownHashes = await database.SeenArtworks.AsNoTracking().Where(x => x.PerceptualHash != null).Select(x => x.PerceptualHash!).ToListAsync(cancellationToken);
            if (knownHashes.Any(x => ArtworkImageFingerprint.IsNearDuplicate(x, perceptualHash))) return false;
        }
        database.Artworks.Add(new ArtworkEntity { Platform = candidate.Platform, ArtworkId = candidate.ArtworkId, NormalizedUrl = normalized, Title = candidate.Title, CharacterName = candidate.CharacterName ?? string.Empty, FranchiseName = candidate.FranchiseName ?? string.Empty, Category = candidate.Category ?? string.Empty, Tags = candidate.Tags ?? string.Empty, Author = candidate.Author, AuthorId = candidate.AuthorId, SourceUrl = candidate.SourceUrl, ThumbnailUrl = candidate.ThumbnailUrl, ThumbnailSha256 = contentSha256, PerceptualHash = perceptualHash, Width = candidate.Width, Height = candidate.Height, SourceMetadata = candidate.SourceMetadata, PublishedAt = candidate.PublishedAt, FetchedAt = candidate.FetchedAt, ReviewStatus = ReviewStatus.Pending, SelectedForReport = false });
        database.SeenArtworks.Add(new SeenArtworkEntity { Platform = candidate.Platform, ArtworkId = candidate.ArtworkId, NormalizedUrl = normalized, ContentSha256 = contentSha256, PerceptualHash = perceptualHash, FirstSeenAt = candidate.FetchedAt });
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetSelectedForReportAsync(Guid artworkId, bool selected, CancellationToken cancellationToken = default)
    {
        var item = await database.Artworks.SingleAsync(x => x.Id == artworkId, cancellationToken);
        if (selected && item.ReviewStatus != ReviewStatus.Confirmed) throw new InvalidOperationException("Only confirmed artwork may be selected for the report.");
        item.SelectedForReport = selected;
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task ConfirmAsync(Guid artworkId, CancellationToken cancellationToken = default)
        => ConfirmAsync(artworkId, "desktop-user", "Desktop artwork confirmation", DateTimeOffset.UtcNow, cancellationToken);

    public async Task ConfirmAsync(Guid artworkId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.Artworks.SingleAsync(x => x.Id == artworkId, cancellationToken);
        if (item.ReviewStatus == ReviewStatus.Archived) throw new InvalidOperationException("Archived artwork cannot be confirmed.");
        var oldStatus = item.ReviewStatus;
        item.ReviewStatus = ReviewStatus.Confirmed;
        AddAction(item.Id, "CONFIRM", actor, reason, now);
        AddRevision(item.Id, "ReviewStatus", oldStatus.ToString(), item.ReviewStatus.ToString(), actor, reason, now);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task ReturnToReviewAsync(Guid artworkId, CancellationToken cancellationToken = default)
        => ReturnToReviewAsync(artworkId, "desktop-user", "Desktop artwork review return", DateTimeOffset.UtcNow, cancellationToken);

    public async Task ReturnToReviewAsync(Guid artworkId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var item = await database.Artworks.SingleAsync(x => x.Id == artworkId, cancellationToken);
        if (item.ReviewStatus == ReviewStatus.Archived) throw new InvalidOperationException("Archived artwork cannot be returned.");
        var oldStatus = item.ReviewStatus;
        item.ReviewStatus = ReviewStatus.Pending;
        item.SelectedForReport = false;
        AddAction(item.Id, "RETURN", actor, reason, now);
        AddRevision(item.Id, "ReviewStatus", oldStatus.ToString(), item.ReviewStatus.ToString(), actor, reason, now);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> BatchConfirmAsync(IEnumerable<Guid> artworkIds, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateActorReason(actor, reason);
        var ids = artworkIds.Distinct().ToArray();
        var items = await database.Artworks.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var item in items.Where(x => x.ReviewStatus == ReviewStatus.Pending))
        {
            var oldStatus = item.ReviewStatus;
            item.ReviewStatus = ReviewStatus.Confirmed;
            AddAction(item.Id, "CONFIRM", actor, reason, now);
            AddRevision(item.Id, "ReviewStatus", oldStatus.ToString(), item.ReviewStatus.ToString(), actor, reason, now);
            changed++;
        }
        await database.SaveChangesAsync(cancellationToken);
        return changed;
    }

    public async Task<int> BatchReturnToReviewAsync(IEnumerable<Guid> artworkIds, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateActorReason(actor, reason);
        var ids = artworkIds.Distinct().ToArray();
        var items = await database.Artworks.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var item in items.Where(x => x.ReviewStatus == ReviewStatus.Confirmed))
        {
            var oldStatus = item.ReviewStatus;
            item.ReviewStatus = ReviewStatus.Pending;
            item.SelectedForReport = false;
            AddAction(item.Id, "RETURN", actor, reason, now);
            AddRevision(item.Id, "ReviewStatus", oldStatus.ToString(), item.ReviewStatus.ToString(), actor, reason, now);
            changed++;
        }
        await database.SaveChangesAsync(cancellationToken);
        return changed;
    }

    public Task<int> BatchDeleteAsync(IEnumerable<Guid> artworkIds, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
        => DeleteManyAsync(artworkIds, actor, reason, now, cancellationToken);

    public async Task DeleteAsync(Guid artworkId, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateActorReason(actor, reason);
        var item = await database.Artworks.SingleAsync(x => x.Id == artworkId, cancellationToken);
        AddAction(item.Id, "DELETE", actor, reason, now);
        AddRevision(item.Id, "Artwork", item.Title, string.Empty, actor, reason, now);
        database.Artworks.Remove(item);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EditMetadataAsync(Guid artworkId, string title, string characterName, string franchiseName, string category, string tags, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateActorReason(actor, reason);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Artwork title is required.");
        var item = await database.Artworks.SingleAsync(x => x.Id == artworkId, cancellationToken);
        if (item.ReviewStatus == ReviewStatus.Archived) throw new InvalidOperationException("Archived artwork cannot be edited.");
        var changes = 0;
        changes += Change(item.Id, "Title", item.Title, title.Trim(), value => item.Title = value, actor, reason, now);
        changes += Change(item.Id, "CharacterName", item.CharacterName, characterName.Trim(), value => item.CharacterName = value, actor, reason, now);
        changes += Change(item.Id, "FranchiseName", item.FranchiseName, franchiseName.Trim(), value => item.FranchiseName = value, actor, reason, now);
        changes += Change(item.Id, "Category", item.Category, category.Trim(), value => item.Category = value, actor, reason, now);
        changes += Change(item.Id, "Tags", item.Tags, tags.Trim(), value => item.Tags = value, actor, reason, now);
        AddAction(item.Id, "EDIT", actor, reason, now);
        await database.SaveChangesAsync(cancellationToken);
        return changes;
    }

    private async Task<int> DeleteManyAsync(IEnumerable<Guid> artworkIds, string actor, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ValidateActorReason(actor, reason);
        var ids = artworkIds.Distinct().ToArray();
        var items = await database.Artworks.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            AddAction(item.Id, "DELETE", actor, reason, now);
            AddRevision(item.Id, "Artwork", item.Title, string.Empty, actor, reason, now);
            database.Artworks.Remove(item);
        }
        await database.SaveChangesAsync(cancellationToken);
        return items.Count;
    }

    private void AddAction(Guid artworkId, string action, string actor, string reason, DateTimeOffset now)
    {
        ValidateActorReason(actor, reason);
        database.ArtworkReviewActions.Add(new ArtworkReviewActionEntity { ArtworkId = artworkId, Action = action, Actor = actor.Trim(), Reason = reason.Trim(), CreatedAt = now });
    }

    private void AddRevision(Guid artworkId, string fieldName, string oldValue, string newValue, string actor, string reason, DateTimeOffset now)
        => database.ArtworkRevisions.Add(new ArtworkRevisionEntity { ArtworkId = artworkId, FieldName = fieldName, OldValue = oldValue, NewValue = newValue, Actor = actor.Trim(), Reason = reason.Trim(), CreatedAt = now });

    private int Change(Guid artworkId, string fieldName, string oldValue, string newValue, Action<string> apply, string actor, string reason, DateTimeOffset now)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return 0;
        apply(newValue);
        AddRevision(artworkId, fieldName, oldValue, newValue, actor, reason, now);
        return 1;
    }

    private static void ValidateActorReason(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Artwork operation requires actor and reason.");
    }

    private static string Normalize(string url) => new Uri(url).GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
