using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>Promotes user-confirmed GAME calendar rows into the confirmed timeline without inventing clock times.</summary>
public sealed class CalendarGameTimelineImportService(QimiaoDailyDbContext database)
{
    private static readonly Regex DateRangePattern = new(@"(?<start>\d{4}-\d{2}-\d{2})\s*(?:至|到|-)\s*(?<end>\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

    public async Task<int> PromoteAsync(CancellationToken cancellationToken = default)
    {
        var calendarEvents = await database.CalendarEvents
            .Where(x => x.Enabled && x.Kind == "GAME")
            .ToListAsync(cancellationToken);
        var existing = (await database.TimelineItems
                .Include(x => x.Evidence)
                .ToListAsync(cancellationToken))
            .Where(x => x.CanonicalIdentity.StartsWith("calendar-game:", StringComparison.Ordinal))
            .ToList();
        var byIdentity = existing.ToDictionary(x => x.CanonicalIdentity, StringComparer.Ordinal);
        var changed = 0;

        foreach (var calendarEvent in calendarEvents)
        {
            var game = GameCode(calendarEvent);
            if (game is null) continue;
            var identity = $"calendar-game:{calendarEvent.Id:N}";
            var range = DateRangePattern.Match(calendarEvent.Detail ?? string.Empty);
            var start = calendarEvent.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = range.Success ? range.Groups["end"].Value : null;
            var title = calendarEvent.Title.Trim();
            var itemType = ActivityType(calendarEvent);
            if (byIdentity.TryGetValue(identity, out var item))
            {
                var expectedEnd = end ?? string.Empty;
                if (item.Title == title && item.ItemType == itemType && item.SourceTime == start && (item.EndExpression ?? string.Empty) == expectedEnd && item.ReviewStatus == ReviewStatus.Confirmed)
                    continue;

                item.Edit(itemType, title, VerificationStatus.VerifiedMultiSource, start, "Asia/Shanghai", null,
                    TimePrecision.DateOnly, DateTimeOffset.UtcNow, null,
                    TimePrecision.DateOnly, TimePrecision.DateOnly,
                    "用户提供的图片日历日期", "用户提供的图片日历日期", start, end);
                item.SetDataProvenance(DataOrigin.Imported, true);
                if (item.Evidence.Count == 0)
                    item.AddEvidence(CreateEvidence(calendarEvent));
                item.Confirm("calendar-import", "用户确认图片日历活动", DateTimeOffset.UtcNow);
                changed++;
                continue;
            }

            var created = new TimelineItem(game, itemType, title, VerificationStatus.VerifiedMultiSource,
                start, "Asia/Shanghai", null, TimePrecision.DateOnly, DateTimeOffset.UtcNow, null,
                TimePrecision.DateOnly, TimePrecision.DateOnly,
                "用户提供的图片日历日期", "用户提供的图片日历日期", start, end);
            created.SetCanonicalIdentity(identity);
            created.SetChangeKind(TimelineChangeKind.New);
            created.SetDataProvenance(DataOrigin.Imported, true);
            created.AddEvidence(CreateEvidence(calendarEvent));
            created.Confirm("calendar-import", "用户确认图片日历活动", DateTimeOffset.UtcNow);
            database.TimelineItems.Add(created);
            changed++;
        }

        if (changed > 0) await database.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private static EvidenceRecord CreateEvidence(CalendarEventEntity calendarEvent)
        => new("user-calendar-image", "calendar-image", "file:///calendar-images-import.json",
            $"{calendarEvent.Title}｜{calendarEvent.Detail ?? calendarEvent.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            "calendar-image-v1", DateTimeOffset.UtcNow, "用户提供的三张日历图片", originalTimezone: "Asia/Shanghai",
            verificationStatus: VerificationStatus.VerifiedMultiSource);

    private static string? GameCode(CalendarEventEntity item)
    {
        var text = $"{item.Title} {item.Detail}";
        if (text.Contains("原神", StringComparison.OrdinalIgnoreCase) || item.Id.ToString().StartsWith("genshin", StringComparison.OrdinalIgnoreCase)) return "GENSHIN";
        if (text.Contains("崩坏：星穹铁道", StringComparison.OrdinalIgnoreCase) || text.Contains("崩铁", StringComparison.OrdinalIgnoreCase) || item.Id.ToString().StartsWith("starrail", StringComparison.OrdinalIgnoreCase)) return "STARRAIL";
        if (text.Contains("异环", StringComparison.OrdinalIgnoreCase) || item.Id.ToString().StartsWith("nte", StringComparison.OrdinalIgnoreCase)) return "NTE";
        return null;
    }

    private static string ActivityType(CalendarEventEntity item)
    {
        var title = item.Title.Trim();
        if (title.StartsWith("祈愿", StringComparison.OrdinalIgnoreCase) || title.Contains("卡池", StringComparison.OrdinalIgnoreCase))
            return "GACHA";
        if (title is "末日幻影" or "虚构叙事" or "幽境危战")
            return "ENDGAME";
        return "EVENT";
    }
}
