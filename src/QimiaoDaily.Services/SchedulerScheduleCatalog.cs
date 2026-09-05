using System.Text.Json;

namespace QimiaoDaily.Services;

public sealed record SchedulerScheduleDefinition(string Mode, int? IntervalMinutes = null, string? LocalTime = null)
{
    public bool IsValid()
    {
        if (string.Equals(Mode, "interval", StringComparison.OrdinalIgnoreCase))
            return IntervalMinutes is > 0 and <= 10080;
        return string.Equals(Mode, "daily", StringComparison.OrdinalIgnoreCase)
            && TimeOnly.TryParseExact(LocalTime, "HH:mm", out _);
    }
}

/// <summary>Loads user-editable scheduler timing without coupling timing rules to task execution.</summary>
public sealed class SchedulerScheduleCatalog
{
    private static readonly HashSet<string> RetiredAutomaticTaskKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "game_data_refresh",
        "birthday_character_refresh",
        "endgame_refresh",
        "nte_official_refresh"
    };

    private static readonly IReadOnlyDictionary<string, SchedulerScheduleDefinition> BuiltIn =
        new Dictionary<string, SchedulerScheduleDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["video_refresh"] = new("interval", 60),
            ["preview_refresh"] = new("interval", 60),
            ["github_bgi_refresh"] = new("daily", LocalTime: "18:05"),
            ["github_scripts_refresh"] = new("daily", LocalTime: "18:05"),
            ["nte_bilibili_refresh"] = new("interval", 60),
            ["artwork_daily_search"] = new("daily", LocalTime: "09:00"),
            ["calendar_refresh"] = new("daily", LocalTime: "00:10"),
            ["archive_cleanup"] = new("daily", LocalTime: "03:59"),
            ["report_build"] = new("daily", LocalTime: "08:00")
        };

    private readonly IReadOnlyDictionary<string, SchedulerScheduleDefinition> _definitions;

    private SchedulerScheduleCatalog(IReadOnlyDictionary<string, SchedulerScheduleDefinition> definitions) => _definitions = definitions;

    public static SchedulerScheduleCatalog Default { get; } = Create(BuiltIn);

    public static bool IsScheduledTask(string taskKey) => BuiltIn.ContainsKey(taskKey);

    public static bool IsRetiredAutomaticTask(string taskKey) => RetiredAutomaticTaskKeys.Contains(taskKey);

    public static SchedulerScheduleCatalog Load(string configDirectory)
    {
        var definitions = new Dictionary<string, SchedulerScheduleDefinition>(BuiltIn, StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(configDirectory, "scheduler.json");
        if (!File.Exists(path)) return Create(definitions);
        try
        {
            using var stream = File.OpenRead(path);
            var overrides = JsonSerializer.Deserialize<Dictionary<string, SchedulerScheduleDefinition>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (overrides is not null)
                foreach (var (taskKey, definition) in overrides)
                    if (definitions.ContainsKey(taskKey) && definition.IsValid()) definitions[taskKey] = definition;
        }
        catch (JsonException)
        {
            // Keep all built-in schedules when a user file is malformed.
        }
        catch (IOException)
        {
            // A transient file access issue must not stop the desktop scheduler.
        }
        return Create(definitions);
    }

    public SchedulerScheduleDefinition Get(string taskKey)
        => _definitions.TryGetValue(taskKey, out var definition) && definition.IsValid()
            ? definition
            : BuiltIn["video_refresh"];

    public DateTimeOffset NextRun(string taskKey, DateTimeOffset nowUtc)
    {
        var definition = Get(taskKey);
        if (string.Equals(definition.Mode, "interval", StringComparison.OrdinalIgnoreCase))
            return nowUtc.AddMinutes(definition.IntervalMinutes!.Value);

        var zone = ResolveChinaTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var localTime = TimeOnly.ParseExact(definition.LocalTime!, "HH:mm");
        var nextDate = localNow.Date + localTime.ToTimeSpan();
        if (nextDate <= localNow.DateTime) nextDate = nextDate.AddDays(1);
        var unspecified = DateTime.SpecifyKind(nextDate, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone));
    }

    private static SchedulerScheduleCatalog Create(IReadOnlyDictionary<string, SchedulerScheduleDefinition> definitions)
        => new(new Dictionary<string, SchedulerScheduleDefinition>(definitions, StringComparer.OrdinalIgnoreCase));

    private static TimeZoneInfo ResolveChinaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
    }
}
