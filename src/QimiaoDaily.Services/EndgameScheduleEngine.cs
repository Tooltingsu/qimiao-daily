namespace QimiaoDaily.Services;

/// <summary>Display precision of a locally calculated endgame occurrence.</summary>
public enum EndgameTimePrecision { Exact, DateOnly }

/// <summary>One independently calculated local rule. AnchorDate deliberately has no implicit time.</summary>
public sealed record EndgameScheduleRule(
    string RuleId,
    string GameCode,
    string DisplayName,
    DateOnly AnchorDate,
    int IntervalDays,
    EndgameTimePrecision Precision,
    TimeOnly? StartTime = null,
    IReadOnlyDictionary<DateOnly, EndgameOccurrenceOverride>? Overrides = null,
    string RuleKind = "INTERVAL");

public sealed record VersionWindow
{
    public VersionWindow(string gameCode, string versionNumber, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(gameCode)) throw new ArgumentException("GameCode is required.", nameof(gameCode));
        if (string.IsNullOrWhiteSpace(versionNumber)) throw new ArgumentException("VersionNumber is required.", nameof(versionNumber));
        if (endDate <= startDate) throw new ArgumentException("EndDate must be after StartDate.", nameof(endDate));
        GameCode = gameCode.Trim();
        VersionNumber = versionNumber.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    public string GameCode { get; }
    public string VersionNumber { get; }
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
}

/// <summary>Changes one scheduled occurrence, keyed by its original calculated start date.</summary>
public sealed record EndgameOccurrenceOverride(
    DateOnly ScheduledStart,
    DateOnly? StartsOn = null,
    TimeOnly? StartTime = null,
    bool Suppressed = false,
    string? Notes = null,
    DateOnly? EndsOn = null,
    TimeOnly? EndTime = null,
    string? VersionNumber = null);

public sealed record EndgameOccurrence(
    string RuleId,
    DateOnly ScheduledStart,
    DateOnly StartsOn,
    EndgameTimePrecision Precision,
    TimeOnly? StartTime,
    string? Notes = null,
    DateOnly? EndsOn = null,
    TimeOnly? EndTime = null,
    string? VersionNumber = null);

/// <summary>Persistence boundary; an adapter can map these DTOs to the V3 domain records.</summary>
public interface IEndgameScheduleStore
{
    Task SaveRuleAsync(EndgameScheduleRule rule, CancellationToken cancellationToken = default);
    Task ReplaceOccurrencesAsync(string ruleId, IReadOnlyList<EndgameOccurrence> occurrences, CancellationToken cancellationToken = default);
}

/// <summary>Pure local recurrence calculation plus an explicit persistence helper.</summary>
public sealed class EndgameScheduleEngine
{
    public IReadOnlyList<EndgameOccurrence> BuildCurrentAndNextTwo(EndgameScheduleRule rule, DateOnly asOf, IReadOnlyList<VersionWindow>? versions = null)
    {
        Validate(rule);
        if (rule.RuleKind.StartsWith("VERSION_", StringComparison.Ordinal))
            return BuildVersionOccurrences(rule, asOf, versions ?? throw new ArgumentException("Version windows are required for this rule.", nameof(versions)));
        return BuildRecurringOccurrences(rule, asOf);
    }

    public EndgameScheduleRule Reanchor(EndgameScheduleRule rule, DateOnly anchorDate) => rule with { AnchorDate = anchorDate };

    public EndgameScheduleRule WithOverride(EndgameScheduleRule rule, EndgameOccurrenceOverride occurrenceOverride)
    {
        Validate(rule);
        if (rule.Precision == EndgameTimePrecision.Exact)
            occurrenceOverride = occurrenceOverride with { StartTime = new TimeOnly(4, 0), EndTime = new TimeOnly(4, 0) };
        if (occurrenceOverride.ScheduledStart < rule.AnchorDate)
            throw new ArgumentException("An occurrence override must target an occurrence on or after the rule anchor.", nameof(occurrenceOverride));
        var overrides = rule.Overrides is null
            ? new Dictionary<DateOnly, EndgameOccurrenceOverride>()
            : new Dictionary<DateOnly, EndgameOccurrenceOverride>(rule.Overrides);
        overrides[occurrenceOverride.ScheduledStart] = occurrenceOverride;
        return rule with { Overrides = overrides };
    }

    public async Task<IReadOnlyList<EndgameOccurrence>> RefreshAsync(
        EndgameScheduleRule rule,
        DateOnly asOf,
        IEndgameScheduleStore store,
        IReadOnlyList<VersionWindow>? versions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var occurrences = BuildCurrentAndNextTwo(rule, asOf, versions);
        await store.SaveRuleAsync(rule, cancellationToken);
        await store.ReplaceOccurrencesAsync(rule.RuleId, occurrences, cancellationToken);
        return occurrences;
    }

    private static void Validate(EndgameScheduleRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.RuleId)) throw new ArgumentException("RuleId is required.", nameof(rule));
        if (rule.IntervalDays <= 0) throw new ArgumentOutOfRangeException(nameof(rule), "IntervalDays must be positive.");
        if (rule.Precision == EndgameTimePrecision.DateOnly && rule.StartTime is not null)
            throw new ArgumentException("DATE_ONLY rules must not have a time component.", nameof(rule));
        if (rule.Precision == EndgameTimePrecision.Exact && rule.StartTime is null)
            throw new ArgumentException("Exact rules require a start time.", nameof(rule));
    }

    private static IReadOnlyList<EndgameOccurrence> BuildRecurringOccurrences(EndgameScheduleRule rule, DateOnly asOf)
    {
        var result = new List<EndgameOccurrence>(3);
        var cursor = rule.AnchorDate;
        var current = (EndgameOccurrence?)null;
        var iterations = 0;

        while (result.Count < 3 && iterations++ < 10000)
        {
            var scheduled = cursor;
            var occurrenceOverride = rule.Overrides is not null && rule.Overrides.TryGetValue(scheduled, out var value) ? value : null;
            var start = occurrenceOverride?.StartsOn ?? scheduled;
            var next = rule.RuleKind == "MONTHLY" ? start.AddMonths(1) : start.AddDays(rule.IntervalDays);
            var end = occurrenceOverride?.EndsOn ?? next;

            if (occurrenceOverride is not { Suppressed: true })
            {
                var occurrence = new EndgameOccurrence(rule.RuleId, scheduled, start, rule.Precision, occurrenceOverride?.StartTime ?? rule.StartTime,
                    occurrenceOverride?.Notes, end, occurrenceOverride?.EndTime ?? rule.StartTime, null);
                if (start <= asOf) current = occurrence;
                else if (current is not null) result.Add(occurrence);
                else result.Add(occurrence);
            }

            cursor = next;
            if (current is not null && result.Count == 2)
                break;
        }

        if (current is not null)
            result.Insert(0, current);
        return result.Take(3).ToList();
    }

    private static IReadOnlyList<EndgameOccurrence> BuildVersionOccurrences(EndgameScheduleRule rule, DateOnly asOf, IReadOnlyList<VersionWindow> versions)
    {
        var windows = versions.Where(x => string.Equals(x.GameCode, rule.GameCode, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.StartDate);
        var result = new List<EndgameOccurrence>(3);
        foreach (var version in windows)
        {
            DateOnly start, end; TimeOnly startTime, endTime;
            switch (rule.RuleKind)
            {
                case "VERSION_STYGIAN": start = version.StartDate.AddDays(7); startTime = new TimeOnly(4, 0); end = version.EndDate; endTime = new TimeOnly(4, 0); break;
                case "VERSION_FRENZIED": start = version.StartDate.AddDays(7); startTime = new TimeOnly(4, 0); end = start.AddDays(10); endTime = new TimeOnly(4, 0); break;
                case "VERSION_BOUNDED": start = version.StartDate; startTime = new TimeOnly(4, 0); end = version.EndDate; endTime = new TimeOnly(4, 0); break;
                default: throw new ArgumentException($"Unsupported version rule kind: {rule.RuleKind}", nameof(rule));
            }
            if (end <= start || end < asOf) continue;
            var scheduled = start;
            var occurrenceOverride = rule.Overrides is not null && rule.Overrides.TryGetValue(scheduled, out var value) ? value : null;
            if (occurrenceOverride is { Suppressed: true }) continue;
            result.Add(new EndgameOccurrence(rule.RuleId, scheduled, occurrenceOverride?.StartsOn ?? start, EndgameTimePrecision.Exact,
                occurrenceOverride?.StartTime ?? startTime, occurrenceOverride?.Notes, occurrenceOverride?.EndsOn ?? end,
                occurrenceOverride?.EndTime ?? endTime, version.VersionNumber));
            if (result.Count == 3) break;
        }
        return result;
    }
}

public static class EndgameScheduleRules
{
    // 2026-08-21 is Friday. DateOnly prevents a fabricated HH:mm value in UI or storage.
    public static EndgameScheduleRule OuterRealm { get; } = new("NTE_OUTER_REALM", "NTE", "异环·轨外之境", new DateOnly(2026, 8, 21), 14, EndgameTimePrecision.DateOnly);
    public static EndgameScheduleRule StarRailMemoryOfChaos { get; } = Exact("STARRAIL_MEMORY_OF_CHAOS", "混沌回忆", new DateOnly(2026, 8, 17));
    public static EndgameScheduleRule StarRailApocalypticShadow { get; } = Exact("STARRAIL_APOCALYPTIC_SHADOW", "末日幻影", new DateOnly(2026, 8, 31));
    public static EndgameScheduleRule StarRailPureFiction { get; } = Exact("STARRAIL_PURE_FICTION", "虚构叙事", new DateOnly(2026, 9, 14));

    public static EndgameScheduleRule GenshinSpiralAbyss { get; } = Monthly("GENSHIN_SPIRAL_ABYSS", "深境螺旋", 16);
    public static EndgameScheduleRule GenshinImaginariumTheater { get; } = Monthly("GENSHIN_IMAGINARIUM_THEATER", "幻想真境剧诗", 1);
    public static EndgameScheduleRule GenshinStygianOnslaught { get; } = Version("GENSHIN_STYGIAN_ONSLAUGHT", "GENSHIN", "幽境危战", "VERSION_STYGIAN");
    public static EndgameScheduleRule GenshinFrenziedOnslaught { get; } = Version("GENSHIN_FRENZIED_ONSLAUGHT", "GENSHIN", "幽境危战·纷乱爆发", "VERSION_FRENZIED");
    public static EndgameScheduleRule StarRailSectorArbitration { get; } = Version("STARRAIL_SECTOR_ARBITRATION", "STARRAIL", "异相仲裁", "VERSION_BOUNDED");

    public static IReadOnlyList<EndgameScheduleRule> All { get; } =
    [
        OuterRealm, StarRailMemoryOfChaos, StarRailApocalypticShadow, StarRailPureFiction,
        GenshinSpiralAbyss, GenshinImaginariumTheater, GenshinStygianOnslaught,
        GenshinFrenziedOnslaught, StarRailSectorArbitration
    ];

    private static EndgameScheduleRule Exact(string id, string name, DateOnly anchor) => new(id, "STARRAIL", name, anchor, 42, EndgameTimePrecision.Exact, new TimeOnly(4, 0));
    private static EndgameScheduleRule Monthly(string id, string name, int day) => new(id, "GENSHIN", name, new DateOnly(2026, 1, day), 31, EndgameTimePrecision.Exact, new TimeOnly(4, 0), RuleKind: "MONTHLY");
    private static EndgameScheduleRule Version(string id, string game, string name, string kind) => new(id, game, name, new DateOnly(2026, 1, 1), 1, EndgameTimePrecision.Exact, new TimeOnly(4, 0), RuleKind: kind);
}
