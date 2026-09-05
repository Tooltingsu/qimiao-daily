using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>EF boundary for locally calculated schedules. DATE_ONLY is persisted as DateOnly, never as a user-facing clock.</summary>
public sealed class DbContextEndgameScheduleStore(QimiaoDailyDbContext database) : IEndgameScheduleStore
{
    public async Task SaveRuleAsync(EndgameScheduleRule rule, CancellationToken cancellationToken = default)
    {
        var entity = await database.EndgameRules.SingleOrDefaultAsync(x => x.RuleKey == rule.RuleId, cancellationToken);
        if (entity is null)
        {
            entity = new EndgameRuleEntity { RuleKey = rule.RuleId };
            database.EndgameRules.Add(entity);
        }

        entity.Game = rule.GameCode;
        entity.Name = rule.DisplayName;
        entity.RuleKind = rule.RuleKind;
        entity.TimePrecision = ToPersistencePrecision(rule.Precision);
        entity.StartTime = rule.Precision == EndgameTimePrecision.DateOnly ? null : rule.StartTime;
        entity.ConfigurationJson = JsonSerializer.Serialize(new EndgameSchedulePersistenceConfiguration(
            rule.IntervalDays, entity.TimePrecision, entity.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            rule.Overrides?.Values.OrderBy(x => x.ScheduledStart).Select(x => new EndgameSchedulePersistedOverride(x.ScheduledStart, x.StartsOn, x.StartTime, x.Suppressed, x.Notes, x.EndsOn, x.EndTime, x.VersionNumber)).ToList() ?? []));

        await database.SaveChangesAsync(cancellationToken);

        var anchors = await database.EndgameAnchors.Where(x => x.RuleId == entity.Id).ToListAsync(cancellationToken);
        var anchor = anchors.FirstOrDefault();
        if (anchor is null)
        {
            anchor = new EndgameAnchorEntity { RuleId = entity.Id };
            database.EndgameAnchors.Add(anchor);
        }
        if (anchors.Count > 1) database.EndgameAnchors.RemoveRange(anchors.Skip(1));
        anchor.AnchorDate = rule.AnchorDate;
        anchor.StartsAt = ToLegacyTimestamp(rule.AnchorDate, rule.Precision, rule.StartTime);
        anchor.Notes = "Local rule anchor";
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceOccurrencesAsync(string ruleId, IReadOnlyList<EndgameOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        var rule = await database.EndgameRules.SingleAsync(x => x.RuleKey == ruleId, cancellationToken);
        var old = await database.EndgameOccurrences.Where(x => x.RuleId == rule.Id).ToListAsync(cancellationToken);
        database.EndgameOccurrences.RemoveRange(old);
        var generatedAt = DateTimeOffset.UtcNow;
        foreach (var (occurrence, sequence) in occurrences.Select((occurrence, sequence) => (occurrence, sequence)))
        {
            var dateOnly = occurrence.Precision == EndgameTimePrecision.DateOnly;
            database.EndgameOccurrences.Add(new EndgameOccurrenceEntity
            {
                RuleId = rule.Id,
                ScheduledDate = occurrence.ScheduledStart,
                OccurrenceDate = occurrence.StartsOn,
                TimePrecision = ToPersistencePrecision(occurrence.Precision),
                StartTime = dateOnly ? null : occurrence.StartTime,
                // Kept for pre-V3 readers only. DateOnly consumers use OccurrenceDate + TimePrecision.
                StartAt = ToLegacyTimestamp(occurrence.StartsOn, occurrence.Precision, occurrence.StartTime),
                EndAt = ToLegacyTimestamp(occurrence.EndsOn ?? occurrence.StartsOn, occurrence.Precision, occurrence.EndTime),
                Sequence = sequence,
                GeneratedAt = generatedAt,
                Notes = occurrence.Notes ?? string.Empty,
                IsOverride = occurrence.ScheduledStart != occurrence.StartsOn || occurrence.StartTime != rule.StartTime ||
                    occurrence.EndTime != rule.StartTime || !string.IsNullOrWhiteSpace(occurrence.Notes)
            });
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private static string ToPersistencePrecision(EndgameTimePrecision precision) => precision == EndgameTimePrecision.DateOnly ? "DATE_ONLY" : "EXACT";
    private static DateTimeOffset ToLegacyTimestamp(DateOnly date, EndgameTimePrecision precision, TimeOnly? time)
        => new(date.ToDateTime(precision == EndgameTimePrecision.DateOnly ? TimeOnly.MinValue : time ?? TimeOnly.MinValue), TimeSpan.FromHours(8));

}

/// <summary>Stable serialized form read by the schedule maintenance service.</summary>
public sealed record EndgameSchedulePersistenceConfiguration(int IntervalDays, string TimePrecision, string? StartTime, IReadOnlyList<EndgameSchedulePersistedOverride> Overrides);
public sealed record EndgameSchedulePersistedOverride(DateOnly ScheduledStart, DateOnly? StartsOn, TimeOnly? StartTime, bool Suppressed, string? Notes, DateOnly? EndsOn = null, TimeOnly? EndTime = null, string? VersionNumber = null);

/// <summary>Maps confirmed qimiao-import.json records to formal V3 entities. Preview only reads QimiaoImportRecordEntity.</summary>
public sealed class DbContextQimiaoImportStore(QimiaoDailyDbContext database) : IQimiaoImportStore
{
    public async Task<IReadOnlyList<QimiaoImportStoredRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
        => await database.ImportRecords.AsNoTracking()
            .Select(x => new QimiaoImportStoredRecord(x.RecordType, x.RecordId, x.NaturalKey, x.PayloadJson))
            .ToListAsync(cancellationToken);

    public Task UpsertAsync(QimiaoImportStoredRecord record, CancellationToken cancellationToken = default)
        => UpsertManyAsync([record], cancellationToken);

    public async Task UpsertManyAsync(IReadOnlyList<QimiaoImportStoredRecord> records, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var record in records) await UpsertFormalRecordAsync(record, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await new ManualDataMigrationService(database).PromoteConfirmedGameCalendarEntriesAsync(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task UpsertFormalRecordAsync(QimiaoImportStoredRecord record, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(record.PayloadJson);
        var value = document.RootElement;
        var mapping = await database.ImportRecords.SingleOrDefaultAsync(x => x.RecordType == record.RecordType && x.RecordId == record.RecordId, ct);
        var (formalId, action) = record.RecordType switch
        {
            "event" => await UpsertEventAsync(value, mapping?.FormalEntityId, ct),
            "banner" => await UpsertBannerAsync(value, mapping?.FormalEntityId, ct),
            "version" => await UpsertVersionAsync(value, mapping?.FormalEntityId, ct),
            "birthday" => await UpsertBirthdayAsync(value, mapping?.FormalEntityId, ct),
            "anniversary" => await UpsertAnniversaryAsync(value, mapping?.FormalEntityId, ct),
            "calendarEvent" => await UpsertCalendarEventAsync(value, mapping?.FormalEntityId, ct),
            _ => throw new ArgumentException($"Unsupported import record type '{record.RecordType}'.", nameof(record))
        };
        if (mapping is null)
        {
            mapping = new QimiaoImportRecordEntity { RecordType = record.RecordType, RecordId = record.RecordId };
            database.ImportRecords.Add(mapping);
        }
        mapping.NaturalKey = record.NaturalKey;
        mapping.PayloadJson = record.PayloadJson;
        mapping.FormalEntityId = formalId;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;
        database.ManualDataAudits.Add(new ManualDataAuditEntity { EntityType = record.RecordType, EntityId = formalId, Action = action });
    }

    private async Task<(Guid, string)> UpsertEventAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var entity = id is { } existing ? await database.ManualEvents.SingleOrDefaultAsync(x => x.Id == existing, ct) : null;
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new ManualEventEntity();
        entity.Game = Required(value, "game"); entity.Name = Required(value, "name"); entity.StartAt = Timestamp(value, "startAt"); entity.EndAt = Timestamp(value, "endAt"); entity.Notes = Optional(value, "notes") ?? string.Empty; entity.Origin = DataOrigin.Imported; entity.UserConfirmed = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (action == "IMPORT_CREATE") database.ManualEvents.Add(entity);
        return (entity.Id, action);
    }

    private async Task<(Guid, string)> UpsertBannerAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var entity = id is { } existing ? await database.Banners.Include(x => x.Characters).SingleOrDefaultAsync(x => x.Id == existing, ct) : null;
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new BannerEntity();
        if (action == "IMPORT_UPDATE") database.BannerCharacters.RemoveRange(entity.Characters);
        entity.Game = Required(value, "game"); entity.Name = Required(value, "name"); entity.Type = Required(value, "type"); entity.CustomType = Optional(value, "customType"); entity.StartAt = Timestamp(value, "startAt"); entity.EndAt = Timestamp(value, "endAt"); entity.Notes = Optional(value, "notes") ?? string.Empty; entity.Origin = DataOrigin.Imported; entity.UserConfirmed = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        var characters = RequiredArray(value, "characters");
        for (var index = 0; index < characters.Count; index++) entity.Characters.Add(new BannerCharacterEntity { Name = characters[index], SortOrder = index });
        if (action == "IMPORT_CREATE") database.Banners.Add(entity);
        return (entity.Id, action);
    }

    private async Task<(Guid, string)> UpsertVersionAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var entity = id is { } existing ? await database.GameVersions.SingleOrDefaultAsync(x => x.Id == existing, ct) : null;
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new GameVersionEntity();
        entity.Game = Required(value, "game"); entity.VersionNumber = Required(value, "versionNumber"); entity.VersionName = Optional(value, "versionName") ?? string.Empty; entity.StartAt = Timestamp(value, "startAt"); entity.EndAt = Timestamp(value, "endAt"); entity.Notes = Optional(value, "notes") ?? string.Empty; entity.Origin = DataOrigin.Imported; entity.UserConfirmed = true; entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (action == "IMPORT_CREATE") database.GameVersions.Add(entity);
        return (entity.Id, action);
    }

    private async Task<(Guid, string)> UpsertBirthdayAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var character = Required(value, "character");
        var game = Required(value, "game");
        var aliases = Optional(value, "aliases") ?? string.Empty;
        var entity = id is { } existing
            ? await database.Birthdays.SingleOrDefaultAsync(x => x.Id == existing, ct)
            : await database.Birthdays.SingleOrDefaultAsync(x => x.Franchise == game &&
                (x.Character == character || (!string.IsNullOrWhiteSpace(aliases) && (x.Character == aliases || x.Aliases == aliases))), ct);
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new BirthdayEntity();
        entity.Character = character; entity.CanonicalCharacterNameZhCn = character; entity.Aliases = aliases; entity.Franchise = game; entity.Month = Integer(value, "month"); entity.Day = Integer(value, "day"); entity.Source = "qimiao-import.json"; entity.SourceTier = "import"; entity.SourceUrl = Optional(value, "sourceUrl") ?? string.Empty; entity.Evidence = Optional(value, "notes") ?? string.Empty; entity.VerificationStatus = VerificationStatus.Unverified; entity.VerifiedAt = DateTimeOffset.UtcNow; entity.Enabled = true; entity.DataOrigin = DataOrigin.Imported; entity.UserConfirmed = true; entity.OriginTrace = "qimiao-import.json";
        if (action == "IMPORT_CREATE") database.Birthdays.Add(entity);
        return (entity.Id, action);
    }

    private async Task<(Guid, string)> UpsertAnniversaryAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var entity = id is { } existing ? await database.Anniversaries.SingleOrDefaultAsync(x => x.Id == existing, ct) : null;
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new AnniversaryEntity();
        entity.Title = Required(value, "title"); entity.StartedOn = DateOnly.Parse(Required(value, "startedOn"), CultureInfo.InvariantCulture); entity.Enabled = true; entity.DataOrigin = DataOrigin.Imported; entity.UserConfirmed = true; entity.Notes = Optional(value, "notes") ?? string.Empty;
        if (action == "IMPORT_CREATE") database.Anniversaries.Add(entity);
        return (entity.Id, action);
    }

    private async Task<(Guid, string)> UpsertCalendarEventAsync(JsonElement value, Guid? id, CancellationToken ct)
    {
        var entity = id is { } existing ? await database.CalendarEvents.SingleOrDefaultAsync(x => x.Id == existing, ct) : null;
        var action = entity is null ? "IMPORT_CREATE" : "IMPORT_UPDATE";
        entity ??= new CalendarEventEntity();
        entity.EventDate = DateOnly.Parse(Required(value, "date"), CultureInfo.InvariantCulture);
        entity.Title = Required(value, "title");
        entity.Kind = Optional(value, "kind") ?? "GAME";
        entity.Detail = Optional(value, "detail");
        entity.Source = "qimiao-import.json";
        entity.SourceUrl = Optional(value, "sourceUrl");
        entity.Enabled = true;
        if (action == "IMPORT_CREATE") database.CalendarEvents.Add(entity);
        return (entity.Id, action);
    }

    private static string Required(JsonElement value, string name) => Optional(value, name) ?? throw new ArgumentException($"Import entry requires '{name}'.");
    private static string? Optional(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject()) if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value.ValueKind switch { JsonValueKind.String => property.Value.GetString(), JsonValueKind.Number => property.Value.GetRawText(), _ => null };
        return null;
    }
    private static DateTimeOffset Timestamp(JsonElement value, string name) => DateTimeOffset.Parse(Required(value, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static int Integer(JsonElement value, string name) => int.Parse(Required(value, name), CultureInfo.InvariantCulture);
    private static IReadOnlyList<string> RequiredArray(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var items) || items.ValueKind != JsonValueKind.Array) throw new ArgumentException($"Import entry requires '{name}' array.");
        return items.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    }
}
