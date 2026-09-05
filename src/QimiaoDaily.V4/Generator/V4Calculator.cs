using QimiaoDaily.Core;
using QimiaoDaily.Services;
using QimiaoDaily.V4.Core;
using CoreAnniversaryRecord = QimiaoDaily.Core.AnniversaryRecord;
using CoreBirthdayRecord = QimiaoDaily.Core.BirthdayRecord;
using V4AnniversaryRecord = QimiaoDaily.V4.Core.AnniversaryRecord;
using V4BirthdayRecord = QimiaoDaily.V4.Core.BirthdayRecord;

namespace QimiaoDaily.V4.Generator;

public sealed class V4Calculator(V4Repository repository)
{
    public IReadOnlyList<CalculatedEndgameRecord> CalculateEndgame(DateOnly asOf)
    {
        var records = new List<CalculatedEndgameRecord>();
        var versions = repository.Read<List<VersionRecord>>("data", "versions.json")
            .Where(x => x.Enabled)
            .Select(x => new VersionWindow(x.Game, x.VersionNumber, ShanghaiClock.Date(x.StartAt), ShanghaiClock.Date(x.EndAt)))
            .ToArray();
        var overrides = repository.Read<List<EndgameOverrideRecord>>("data", "endgame-overrides.json")
            .GroupBy(x => x.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (IReadOnlyDictionary<DateOnly, EndgameOccurrenceOverride>)x.ToDictionary(
                item => item.ScheduledStart,
                item => new EndgameOccurrenceOverride(item.ScheduledStart, item.StartsOn, item.StartTime, item.Suppressed, item.Notes, item.EndsOn, item.EndTime)),
                StringComparer.OrdinalIgnoreCase);

        var engine = new EndgameScheduleEngine();
        foreach (var item in repository.Read<List<EndgameRuleRecord>>("data", "endgame-rules.json").Where(x => x.Enabled))
        {
            overrides.TryGetValue(item.RuleId, out var ruleOverrides);
            var precision = item.TimePrecision.Equals("DATE_ONLY", StringComparison.OrdinalIgnoreCase) ? EndgameTimePrecision.DateOnly : EndgameTimePrecision.Exact;
            var rule = new EndgameScheduleRule(item.RuleId, item.Game, item.Name, item.AnchorDate, item.IntervalDays, precision, item.StartTime, ruleOverrides, item.RuleKind);
            var occurrences = engine.BuildCurrentAndNextTwo(rule, asOf, item.RuleKind.StartsWith("VERSION_", StringComparison.Ordinal) ? versions : null);
            records.AddRange(occurrences.Select(x => new CalculatedEndgameRecord(
                item.RuleId, item.Game, item.Name, x.ScheduledStart, x.StartsOn, x.Precision == EndgameTimePrecision.DateOnly ? "DATE_ONLY" : "EXACT",
                x.StartTime, x.EndsOn, x.EndTime, x.VersionNumber, x.Notes ?? string.Empty)));
        }

        repository.Write(records.OrderBy(x => x.StartsOn).ThenBy(x => x.RuleId).ToList(), "generated", "endgame.json");
        return records;
    }

    public IReadOnlyList<CalendarRecord> CalculateCalendar(int year)
    {
        var birthdays = repository.Read<List<V4BirthdayRecord>>("data", "birthdays.json")
            .Select(x => new CoreBirthdayRecord(x.Character, x.Franchise, x.Month, x.Day, x.Source, x.SourceUrl,
                x.Evidence, VerificationStatus.VerifiedOfficial, DateTimeOffset.UtcNow, x.Enabled))
            .ToArray();
        var anniversaries = repository.Read<List<V4AnniversaryRecord>>("data", "anniversaries.json")
            .Select(x => new CoreAnniversaryRecord(x.Title, x.StartedOn, x.Enabled))
            .ToArray();
        var customEvents = repository.Read<List<ManualCalendarEventRecord>>("data", "calendar-events.json")
            .Where(x => x.Enabled && !x.Kind.Equals("GAME", StringComparison.OrdinalIgnoreCase) && x.EventDate.Year == year)
            .ToArray();
        var records = new List<CalendarRecord>();

        for (var date = new DateOnly(year, 1, 1); date.Year == year; date = date.AddDays(1))
        {
            records.AddRange(ChineseCalendarEngine.Occurrences(date, birthdays, anniversaries)
                .Select(x => new CalendarRecord(x.Date, x.Kind, x.Title, x.Detail ?? string.Empty)));
            records.AddRange(customEvents.Where(x => x.EventDate == date)
                .Select(x => new CalendarRecord(x.EventDate, x.Kind, x.Title, x.Detail)));
        }

        repository.Write(records.OrderBy(x => x.Date).ThenBy(x => x.Kind).ThenBy(x => x.Title).ToList(), "generated", "calendar.json");
        return records;
    }
}
