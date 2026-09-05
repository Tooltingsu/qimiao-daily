using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Publishing;

public sealed class PublishWindowGuard(V4Settings settings)
{
    public (bool ShouldPublish, DateOnly ReportDate, string Reason) Evaluate(DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        if (!TimeOnly.TryParse(settings.PublishTime, out var publishTime))
            return (false, DateOnly.FromDateTime(local.DateTime), "Invalid publishTime setting.");
        var date = DateOnly.FromDateTime(local.DateTime);
        var target = date.ToDateTime(publishTime);
        var current = local.DateTime;
        if (current < target) return (false, date, "Before configured publish time.");
        if (current > target.AddMinutes(30)) return (false, date, "Outside the 30-minute watchdog window.");
        return (true, date, "Inside watchdog window.");
    }
}
