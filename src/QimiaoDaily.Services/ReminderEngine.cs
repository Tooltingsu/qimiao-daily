using QimiaoDaily.Core;

namespace QimiaoDaily.Services;

public enum ReminderKind { StartsToday, StartsTomorrow, EndsToday, EndsTomorrow, NewVideo, PreviewLiveToday }
public sealed record Reminder(Guid TimelineItemId, ReminderKind Kind, string Text);

public sealed class ReminderEngine
{
    private static readonly TimeZoneInfo Shanghai = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    public IReadOnlyList<Reminder> Build(IEnumerable<TimelineItem> items, DateTimeOffset now)
    {
        var localDate = TimeZoneInfo.ConvertTime(now, Shanghai).Date;
        var result = new List<Reminder>();
        foreach (var item in items.Where(ReportEligibility.CanInclude))
        {
            if (item.ItemType == "VIDEO" && item.NormalizedTime is { } video && ToDate(video) == localDate) result.Add(new(item.Id, ReminderKind.NewVideo, $"{item.GameCode} 发布视频【{item.Title}】"));
            if (item.ItemType == "PREVIEW_LIVE" && item.NormalizedTime is { } live && ToDate(live) == localDate) result.Add(new(item.Id, ReminderKind.PreviewLiveToday, $"{item.GameCode} 前瞻【{item.Title}】今日直播"));
            AddTimeReminder(item.NormalizedTime, localDate, item, true, result);
            AddTimeReminder(item.EndAt, localDate, item, false, result);
        }
        return result;
    }
    private static void AddTimeReminder(DateTimeOffset? instant, DateTime today, TimelineItem item, bool start, List<Reminder> output)
    { if (instant is null) return; var date = ToDate(instant.Value); if (date == today) output.Add(new(item.Id,start?ReminderKind.StartsToday:ReminderKind.EndsToday,$"{item.GameCode} {item.Title}{(start?"今日开始":"今日结束")}")); else if(date==today.AddDays(1))output.Add(new(item.Id,start?ReminderKind.StartsTomorrow:ReminderKind.EndsTomorrow,$"{item.GameCode} {item.Title}{(start?"明日开始":"明日结束")}")); }
    private static DateTime ToDate(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Shanghai).Date;
}
