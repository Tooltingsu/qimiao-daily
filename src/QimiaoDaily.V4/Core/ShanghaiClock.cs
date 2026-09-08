namespace QimiaoDaily.V4.Core;

public static class ShanghaiClock
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
    public static DateOnly Date(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }
    public static (DateTimeOffset Start, DateTimeOffset End) BgiWindow(DateOnly date)
        => (At(date.AddDays(-1), new TimeOnly(18, 0)), At(date, new TimeOnly(18, 0)));

    // The video portion of the daily report follows the same fixed Shanghai
    // cutoff as BGI: yesterday 18:00 inclusive through today 18:00 exclusive.
    public static (DateTimeOffset Start, DateTimeOffset End) VideoWindow(DateOnly date)
        => (At(date.AddDays(-1), new TimeOnly(18, 0)), At(date, new TimeOnly(18, 0)));
}
