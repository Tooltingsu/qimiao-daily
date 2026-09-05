using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Validator;

public sealed class V4Validator(V4Repository repository)
{
    private static readonly (string Data, string Schema)[] ManualContracts =
    [
        ("activities.json", "activities.schema.json"),
        ("banners.json", "banners.schema.json"),
        ("versions.json", "versions.schema.json"),
        ("endgame-rules.json", "endgame-rules.schema.json"),
        ("endgame-overrides.json", "endgame-overrides.schema.json"),
        ("birthdays.json", "birthdays.schema.json"),
        ("anniversaries.json", "anniversaries.schema.json"),
        ("calendar-events.json", "calendar-events.schema.json"),
        ("settings.json", "settings.schema.json")
    ];

    public ValidationResult ValidateAll()
    {
        var issues = new List<ValidationIssue>();
        foreach (var contract in ManualContracts)
        {
            var data = repository.PathFor("data", contract.Data);
            var schema = repository.PathFor("schemas", contract.Schema);
            if (!File.Exists(data)) issues.Add(new("ERROR", "data/" + contract.Data, "$", "Data file is missing."));
            else if (!File.Exists(schema)) issues.Add(new("ERROR", "schemas/" + contract.Schema, "$", "Schema file is missing."));
            else issues.AddRange(JsonSchemaSubsetValidator.Validate(data, schema, "data/" + contract.Data));
        }

        if (issues.Count == 0)
        {
            try { ValidateSemantics(issues); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException)
            { issues.Add(new("ERROR", "data/", "$", "Invalid typed date/time/value: " + ex.GetType().Name)); }
        }
        return new(issues.All(x => x.Level != "ERROR"), issues);
    }

    private void ValidateSemantics(List<ValidationIssue> issues)
    {
        var activities = repository.Read<List<ManualEventRecord>>("data", "activities.json");
        for (var i = 0; i < activities.Count; i++)
            if (activities[i].StartAt >= activities[i].EndAt) issues.Add(Error("activities.json", i, "startAt must be before endAt."));

        var banners = repository.Read<List<BannerRecord>>("data", "banners.json");
        for (var i = 0; i < banners.Count; i++)
        {
            if (banners[i].StartAt >= banners[i].EndAt) issues.Add(Error("banners.json", i, "startAt must be before endAt."));
            if (string.IsNullOrWhiteSpace(banners[i].Game)) issues.Add(Error("banners.json", i, "game is required."));
            if (banners[i].Characters.Count == 0 || banners[i].Characters.Any(string.IsNullOrWhiteSpace)) issues.Add(Error("banners.json", i, "at least one character is required."));
        }

        var versions = repository.Read<List<VersionRecord>>("data", "versions.json");
        for (var i = 0; i < versions.Count; i++)
            if (versions[i].StartAt >= versions[i].EndAt) issues.Add(Error("versions.json", i, "startAt must be before endAt."));
        foreach (var group in versions.Where(x => x.Enabled).GroupBy(x => x.Game))
        {
            var windows = group.OrderBy(x => x.StartAt).ToArray();
            for (var i = 1; i < windows.Length; i++)
                if (windows[i].StartAt < windows[i - 1].EndAt)
                    issues.Add(new("ERROR", "data/versions.json", "$", "Overlapping versions for " + group.Key));
        }
        var birthdays = repository.Read<List<BirthdayRecord>>("data", "birthdays.json");
        for (var i = 0; i < birthdays.Count; i++)
        {
            var b = birthdays[i];
            if (!b.Enabled && b.Month == 0 && b.Day == 0) continue;
            if (b.Month is < 1 or > 12 || b.Day < 1 || b.Day > DateTime.DaysInMonth(2000, b.Month))
                issues.Add(Error("birthdays.json", i, "Invalid birthday month/day (Feb 29 allowed)."));
        }

        var rules = repository.Read<List<EndgameRuleRecord>>("data", "endgame-rules.json");
        if (rules.GroupBy(x => x.RuleId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            issues.Add(new("ERROR", "data/endgame-rules.json", "$", "ruleId values must be unique."));
        foreach (var rule in rules)
        {
            if ((rule.TimePrecision == "DATE_ONLY" && rule.StartTime is not null) || (rule.TimePrecision == "EXACT" && rule.StartTime is null))
                issues.Add(new("ERROR", "data/endgame-rules.json", rule.RuleId, "Time precision and startTime disagree."));
        }
        var overrides = repository.Read<List<EndgameOverrideRecord>>("data", "endgame-overrides.json");
        if (overrides.GroupBy(x => (x.RuleId, x.ScheduledStart)).Any(x => x.Count() > 1))
            issues.Add(new("ERROR", "data/endgame-overrides.json", "$", "Duplicate occurrence override."));
        foreach (var item in overrides)
        {
            var rule = rules.SingleOrDefault(x => x.RuleId == item.RuleId);
            if (rule is null || item.EndsOn < (item.StartsOn ?? item.ScheduledStart) ||
                (rule.TimePrecision == "DATE_ONLY" && (item.StartTime is not null || item.EndTime is not null)))
                issues.Add(new("ERROR", "data/endgame-overrides.json", item.RuleId, "Invalid override rule/date/precision."));
        }

        var settings = repository.Read<V4Settings>("data", "settings.json");
        if (settings.TimeZone != "Asia/Shanghai") issues.Add(new("ERROR", "data/settings.json", "$.timeZone", "Business timezone must be Asia/Shanghai."));
        if (!TimeOnly.TryParse(settings.PublishTime, out _)) issues.Add(new("ERROR", "data/settings.json", "$.publishTime", "publishTime must be HH:mm."));
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone); }
        catch { issues.Add(new("ERROR", "data/settings.json", "$.timeZone", "Unknown IANA/Windows time zone.")); }
    }

    private static ValidationIssue Error(string file, int index, string message) => new("ERROR", "data/" + file, $"$[{index}]", message);
}
