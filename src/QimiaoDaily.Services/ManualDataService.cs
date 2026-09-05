using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed record VersionSaveResult(GameVersionEntity? Version, bool HasOverlapWarning);

public sealed class ManualDataService(QimiaoDailyDbContext database)
{
    public async Task<ManualEventEntity> CreateEventAsync(ManualEventInput input, CancellationToken ct = default)
    {
        input = NormalizeEventTimes(input);
        Validate(input.Game, input.Name, input.StartAt, input.EndAt);
        var entity = new ManualEventEntity { Game = input.Game.Trim(), Name = input.Name.Trim(), StartAt = input.StartAt, EndAt = input.EndAt, Notes = input.Notes?.Trim() ?? string.Empty, Origin = DataOrigin.Manual, UserConfirmed = true };
        database.ManualEvents.Add(entity);
        Audit("ManualEvent", entity.Id, "CREATE");
        await database.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<ManualEventEntity> UpdateEventAsync(Guid id, ManualEventInput input, CancellationToken ct = default)
    {
        input = NormalizeEventTimes(input);
        Validate(input.Game, input.Name, input.StartAt, input.EndAt);
        var entity = await database.ManualEvents.SingleAsync(x => x.Id == id, ct);
        entity.Game = input.Game.Trim(); entity.Name = input.Name.Trim(); entity.StartAt = input.StartAt; entity.EndAt = input.EndAt; entity.Notes = input.Notes?.Trim() ?? string.Empty; entity.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("ManualEvent", entity.Id, "UPDATE");
        await database.SaveChangesAsync(ct);
        return entity;
    }

    public async Task ArchiveEventAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await database.ManualEvents.SingleAsync(x => x.Id == id, ct);
        entity.Archived = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("ManualEvent", entity.Id, "ARCHIVE");
        await database.SaveChangesAsync(ct);
    }

    public async Task<BannerEntity> CreateBannerAsync(BannerInput input, CancellationToken ct = default)
    {
        Validate(input.Game, input.Name, input.StartAt, input.EndAt);
        if (string.IsNullOrWhiteSpace(input.Type) && string.IsNullOrWhiteSpace(input.CustomType)) throw new ArgumentException("Banner type is required.", nameof(input));
        var entity = new BannerEntity { Game = input.Game.Trim(), Name = input.Name.Trim(), Type = input.Type.Trim(), CustomType = string.IsNullOrWhiteSpace(input.CustomType) ? null : input.CustomType.Trim(), StartAt = input.StartAt, EndAt = input.EndAt, Notes = input.Notes?.Trim() ?? string.Empty, Origin = DataOrigin.Manual, UserConfirmed = true };
        foreach (var (name, index) in input.Characters.Select((name, index) => (name, index)))
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Banner character name is required.", nameof(input));
            entity.Characters.Add(new BannerCharacterEntity { Name = name.Trim(), SortOrder = index });
        }
        database.Banners.Add(entity);
        Audit("Banner", entity.Id, "CREATE");
        await database.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<BannerEntity> UpdateBannerAsync(Guid id, BannerInput input, CancellationToken ct = default)
    {
        Validate(input.Game, input.Name, input.StartAt, input.EndAt);
        if (string.IsNullOrWhiteSpace(input.Type) && string.IsNullOrWhiteSpace(input.CustomType)) throw new ArgumentException("Banner type is required.", nameof(input));

        var entity = await database.Banners.Include(x => x.Characters).SingleAsync(x => x.Id == id, ct);
        entity.Game = input.Game.Trim(); entity.Name = input.Name.Trim(); entity.Type = input.Type.Trim();
        entity.CustomType = string.IsNullOrWhiteSpace(input.CustomType) ? null : input.CustomType.Trim();
        entity.StartAt = input.StartAt; entity.EndAt = input.EndAt; entity.Notes = input.Notes?.Trim() ?? string.Empty;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        database.BannerCharacters.RemoveRange(entity.Characters);
        foreach (var (name, index) in input.Characters.Select((name, index) => (name, index)))
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Banner character name is required.", nameof(input));
            database.BannerCharacters.Add(new BannerCharacterEntity { BannerId = entity.Id, Name = name.Trim(), SortOrder = index });
        }
        Audit("Banner", entity.Id, "UPDATE");
        await database.SaveChangesAsync(ct);
        return entity;
    }

    public async Task ArchiveBannerAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await database.Banners.SingleAsync(x => x.Id == id, ct);
        entity.Archived = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("Banner", entity.Id, "ARCHIVE");
        await database.SaveChangesAsync(ct);
    }

    public async Task DeleteBannerAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await database.Banners.SingleAsync(x => x.Id == id, ct);
        Audit("Banner", entity.Id, "DELETE");
        database.Banners.Remove(entity);
        await database.SaveChangesAsync(ct);
    }

    public async Task<VersionSaveResult> SaveVersionAsync(GameVersionInput input, bool forceOverlap, CancellationToken ct = default)
    {
        Validate(input.Game, input.VersionNumber, input.StartAt, input.EndAt);
        var game = input.Game.Trim();
        // SQLite cannot translate DateTimeOffset ordering. Filter the small per-game set in SQL,
        // then apply the interval comparison consistently in memory.
        var existingVersions = await database.GameVersions.Where(x => x.Game == game && !x.Archived).ToListAsync(ct);
        var overlaps = existingVersions.Any(x => x.StartAt < input.EndAt && input.StartAt < x.EndAt);
        if (overlaps && !forceOverlap) return new VersionSaveResult(null, true);
        var entity = new GameVersionEntity { Game = game, VersionNumber = input.VersionNumber.Trim(), VersionName = input.VersionName?.Trim() ?? string.Empty, StartAt = input.StartAt, EndAt = input.EndAt, Notes = input.Notes?.Trim() ?? string.Empty, Origin = DataOrigin.Manual, UserConfirmed = true };
        database.GameVersions.Add(entity);
        Audit("GameVersion", entity.Id, "CREATE");
        await database.SaveChangesAsync(ct);
        return new VersionSaveResult(entity, false);
    }

    public async Task<VersionSaveResult> UpdateVersionAsync(Guid id, GameVersionInput input, bool forceOverlap, CancellationToken ct = default)
    {
        Validate(input.Game, input.VersionNumber, input.StartAt, input.EndAt);
        var entity = await database.GameVersions.SingleAsync(x => x.Id == id, ct);
        var game = input.Game.Trim();
        var existingVersions = await database.GameVersions
            .Where(x => x.Game == game && !x.Archived && x.Id != id)
            .ToListAsync(ct);
        var overlaps = existingVersions.Any(x => x.StartAt < input.EndAt && input.StartAt < x.EndAt);
        if (overlaps && !forceOverlap) return new VersionSaveResult(null, true);

        entity.Game = game; entity.VersionNumber = input.VersionNumber.Trim(); entity.VersionName = input.VersionName?.Trim() ?? string.Empty;
        entity.StartAt = input.StartAt; entity.EndAt = input.EndAt; entity.Notes = input.Notes?.Trim() ?? string.Empty;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("GameVersion", entity.Id, "UPDATE");
        await database.SaveChangesAsync(ct);
        return new VersionSaveResult(entity, false);
    }

    public async Task ArchiveVersionAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await database.GameVersions.SingleAsync(x => x.Id == id, ct);
        entity.Archived = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("GameVersion", entity.Id, "ARCHIVE");
        await database.SaveChangesAsync(ct);
    }

    public async Task DeleteVersionAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await database.GameVersions.SingleAsync(x => x.Id == id, ct);
        Audit("GameVersion", entity.Id, "DELETE");
        database.GameVersions.Remove(entity);
        await database.SaveChangesAsync(ct);
    }

    private void Audit(string entityType, Guid entityId, string action) => database.ManualDataAudits.Add(new ManualDataAuditEntity { EntityType = entityType, EntityId = entityId, Action = action });

    private static ManualEventInput NormalizeEventTimes(ManualEventInput input)
    {
        // The manual activity convention is date-based: both boundaries are
        // displayed and persisted at Shanghai 04:00 rather than mixing
        // arbitrary imported clock values into the activity board.
        var start = AtFour(input.StartAt);
        var end = AtFour(input.EndAt);
        return input with { StartAt = start, EndAt = end };
    }

    private static DateTimeOffset AtFour(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, 4, 0, 0, value.Offset);

    private static void Validate(string game, string name, DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (string.IsNullOrWhiteSpace(game)) throw new ArgumentException("Game is required.", nameof(game));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (endAt <= startAt) throw new ArgumentException("End time must be after start time.", nameof(endAt));
    }
}
