namespace QimiaoDaily.Core;

public static class TimeDisplay
{
    public static string Format(DateTimeOffset? startAt, DateTimeOffset? endAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var localStart = startAt is null ? (DateTimeOffset?)null : TimeZoneInfo.ConvertTime(startAt.Value, zone);
        var localEnd = endAt is null ? (DateTimeOffset?)null : TimeZoneInfo.ConvertTime(endAt.Value, zone);

        if (localEnd is null) return "结束时间待确认";
        if (localEnd <= localNow) return $"已结束 {Duration(localEnd.Value, localNow)}";
        if (localStart is not null && localStart > localNow) return $"距开始 {Duration(localNow, localStart.Value)}";
        return $"剩余 {Duration(localNow, localEnd.Value)}";
    }

    private static string Duration(DateTimeOffset from, DateTimeOffset to)
    {
        var totalHours = Math.Max(0, (to - from).TotalHours);
        var hours = (int)Math.Floor(totalHours);
        return $"{hours / 24}天{hours % 24}小时";
    }
}
