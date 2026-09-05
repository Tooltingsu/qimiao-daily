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
}
