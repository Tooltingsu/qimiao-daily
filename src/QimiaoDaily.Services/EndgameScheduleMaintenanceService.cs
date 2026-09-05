using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>
/// Writes an explicit re-anchor or occurrence override through the V3 rule store.
/// Every operation is scoped to one database RuleId; replacing occurrences never touches another rule.
/// </summary>
public sealed class EndgameScheduleMaintenanceService(QimiaoDailyDbContext database)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<EndgameOccurrence>> ReanchorAsync(
        Guid ruleId,
        DateOnly anchorDate,
        DateOnly asOf,
        IReadOnlyList<VersionWindow>? versions = null,
        CancellationToken cancellationToken = default)
    {
        // A first re-anchor is valid even when the rule has no persisted anchor yet.
        var rule = await LoadRuleAsync(ruleId, cancellationToken, anchorDate);
        if (rule.RuleKind.StartsWith("VERSION_", StringComparison.Ordinal))
            throw new InvalidOperationException("Version-dependent endgame rules do not support a standalone re-anchor.");

        return await PersistAsync(rule with { AnchorDate = anchorDate }, asOf, versions, cancellationToken);
    }

    public async Task<IReadOnlyList<EndgameOccurrence>> OverrideAsync(
        Guid ruleId,
        EndgameOccurrenceOverride occurrenceOverride,
        DateOnly asOf,
        IReadOnlyList<VersionWindow>? versions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrenceOverride);
        var rule = await LoadRuleAsync(ruleId, cancellationToken);
        var updated = new EndgameScheduleEngine().WithOverride(rule, occurrenceOverride);
        return await PersistAsync(updated, asOf, versions, cancellationToken);
    }

    /// <summary>
    /// Rebuilds only version-dependent rules after a manual game-version change.
    /// </summary>
    public async Task<int> RefreshVersionDependentRulesAsync(
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var rules = await database.EndgameRules
            .Where(x => x.Enabled && x.RuleKind.StartsWith("VERSION_"))
            .ToListAsync(cancellationToken);
        if (rules.Count == 0) return 0;

        var versions = await database.GameVersions
            .Where(x => !x.Archived && x.UserConfirmed)
            .ToListAsync(cancellationToken);
        versions = versions.OrderBy(x => x.StartAt).ToList();
        var windows = versions
            .Select(x => new VersionWindow(
                x.Game,
                x.VersionNumber,
                DateOnly.FromDateTime(x.StartAt.DateTime),
                DateOnly.FromDateTime(x.EndAt.DateTime)))
            .ToArray();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var store = new DbContextEndgameScheduleStore(database);
            foreach (var entity in rules)
            {
                var rule = await LoadRuleAsync(entity.Id, cancellationToken);
                await new EndgameScheduleEngine().RefreshAsync(rule, asOf, store, windows, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return rules.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Rebuilds the current and next two occurrences for every enabled local rule.</summary>
    public async Task<int> RefreshAllRulesAsync(
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var rules = await database.EndgameRules
            .Where(x => x.Enabled)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0) return 0;

        var versions = (await database.GameVersions
                .Where(x => !x.Archived && x.UserConfirmed)
                .ToListAsync(cancellationToken))
            .OrderBy(x => x.StartAt)
            .Select(x => new VersionWindow(
                x.Game,
                x.VersionNumber,
                DateOnly.FromDateTime(x.StartAt.DateTime),
                DateOnly.FromDateTime(x.EndAt.DateTime)))
            .ToArray();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var store = new DbContextEndgameScheduleStore(database);
            var generated = 0;
            foreach (var entity in rules)
            {
                var rule = await LoadRuleAsync(entity.Id, cancellationToken);
                var occurrences = await new EndgameScheduleEngine().RefreshAsync(
                    rule, asOf, store,
                    rule.RuleKind.StartsWith("VERSION_", StringComparison.Ordinal) ? versions : null,
                    cancellationToken);
                generated += occurrences.Count;
            }

            await transaction.CommitAsync(cancellationToken);
            return generated;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlyList<EndgameOccurrence>> PersistAsync(
        EndgameScheduleRule rule,
        DateOnly asOf,
        IReadOnlyList<VersionWindow>? versions,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await new EndgameScheduleEngine().RefreshAsync(rule, asOf, new DbContextEndgameScheduleStore(database), versions, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<EndgameScheduleRule> LoadRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken,
        DateOnly? fallbackAnchor = null)
    {
        var entity = await database.EndgameRules.SingleAsync(x => x.Id == ruleId, cancellationToken);
        var defaultRule = EndgameScheduleRules.All.SingleOrDefault(x => string.Equals(x.RuleId, entity.RuleKey, StringComparison.Ordinal));
        var configuration = JsonSerializer.Deserialize<EndgameSchedulePersistenceConfiguration>(entity.ConfigurationJson, JsonOptions);
        var precision = string.Equals(entity.TimePrecision, "DATE_ONLY", StringComparison.OrdinalIgnoreCase)
            ? EndgameTimePrecision.DateOnly : EndgameTimePrecision.Exact;
        TimeOnly? time = precision == EndgameTimePrecision.DateOnly
            ? null
            : new TimeOnly(4, 0);
        var anchor = (await database.EndgameAnchors.Where(x => x.RuleId == ruleId).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.AnchorDate ?? DateOnly.MinValue).FirstOrDefault()?.AnchorDate
            ?? defaultRule?.AnchorDate
            ?? fallbackAnchor
            ?? throw new InvalidOperationException("The endgame rule requires an anchor before maintenance.");
        var interval = configuration?.IntervalDays > 0 ? configuration.IntervalDays : defaultRule?.IntervalDays ?? 0;
        var overrides = (configuration?.Overrides ?? [])
            .ToDictionary(x => x.ScheduledStart, x => new EndgameOccurrenceOverride(x.ScheduledStart, x.StartsOn,
                precision == EndgameTimePrecision.Exact ? new TimeOnly(4, 0) : null, x.Suppressed, x.Notes, x.EndsOn,
                precision == EndgameTimePrecision.Exact ? new TimeOnly(4, 0) : null, x.VersionNumber));
        return new EndgameScheduleRule(entity.RuleKey, entity.Game, entity.Name, anchor, interval, precision, time, overrides, entity.RuleKind);
    }

    private static TimeOnly? ParseTime(string? value)
        => TimeOnly.TryParse(value, out var time) ? time : null;
}
