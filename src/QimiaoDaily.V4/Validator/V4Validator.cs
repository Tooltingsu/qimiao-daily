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

        if (issues.Count == 0) ValidateSemantics(issues);
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

        var rules = repository.Read<List<EndgameRuleRecord>>("data", "endgame-rules.json");
        if (rules.GroupBy(x => x.RuleId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            issues.Add(new("ERROR", "data/endgame-rules.json", "$", "ruleId values must be unique."));

        var settings = repository.Read<V4Settings>("data", "settings.json");
        if (!TimeOnly.TryParse(settings.PublishTime, out _)) issues.Add(new("ERROR", "data/settings.json", "$.publishTime", "publishTime must be HH:mm."));
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone); }
        catch { issues.Add(new("ERROR", "data/settings.json", "$.timeZone", "Unknown IANA/Windows time zone.")); }
    }

    private static ValidationIssue Error(string file, int index, string message) => new("ERROR", "data/" + file, $"$[{index}]", message);
}
