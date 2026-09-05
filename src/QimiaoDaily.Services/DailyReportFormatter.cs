using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>A persisted calculated occurrence enriched with its display rule.</summary>
public sealed record EndgameReportOccurrence(string Game, string Name, DateTimeOffset StartAt, bool IsDateOnly);

/// <summary>Formats only verified, confirmed records for the user-facing daily report.</summary>
public static class DailyReportFormatter
{
    private static readonly TimeZoneInfo Shanghai = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    public const string SectionSeparator = "——————————————————";

    public static string DateLine(DateOnly date)
    {
        var dayName = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => "日",
            DayOfWeek.Monday => "一",
            DayOfWeek.Tuesday => "二",
            DayOfWeek.Wednesday => "三",
            DayOfWeek.Thursday => "四",
            DayOfWeek.Friday => "五",
            _ => "六"
        };
        return $"今天是{date:yyyy年M月d日}，星期{dayName}";
    }

    public static string SectionTitle(string key) => key switch
    {
        "calendar" => "节日与纪念日",
        "games" => "游戏活动预览",
        "bgi" => "BGI 更新",
        "artwork" => "美图分享",
        _ => "其他日报内容"
    };

    public static string FormatCalendar(IEnumerable<CalendarOccurrence> occurrences)
        => string.Join(Environment.NewLine, occurrences
            .OrderBy(CalendarSortOrder)
            .Select(CalendarLine));

    private static int CalendarSortOrder(CalendarOccurrence occurrence) => occurrence.Kind switch
    {
        "SOLAR_TERM" => 0,
        "FESTIVAL" => 1,
        "BIRTHDAY" => 2,
        "ANNIVERSARY" => 3,
        _ => 4
    };

    private static string CalendarLine(CalendarOccurrence occurrence) => occurrence.Kind switch
    {
        "SOLAR_TERM" => "今天是二十四节气 " + occurrence.Title,
        "FESTIVAL" => "今天是节日 " + occurrence.Title,
        "BIRTHDAY" => string.IsNullOrWhiteSpace(occurrence.Detail)
            ? $"今天是【{occurrence.Title}】的生日"
            : $"今天是【{GameName(occurrence.Detail)} {occurrence.Title}】的生日",
        "ANNIVERSARY" => $"今天是【{occurrence.Title}】{occurrence.Detail ?? string.Empty}纪念日",
        "MEMORIAL" => "今天是纪念日 " + occurrence.Title,
        _ => "今天是 " + occurrence.Title + (string.IsNullOrWhiteSpace(occurrence.Detail) ? string.Empty : " " + occurrence.Detail)
    };

    public static string FormatGames(IEnumerable<TimelineItem> source, DateOnly reportDate, DateTimeOffset? now = null)
    {
        var eligible = source.Where(x => x.DataOrigin != DataOrigin.LegacyAuto && ReportEligibility.CanInclude(x)).ToList();
        var localNow = ToShanghai(now ?? DateTimeOffset.UtcNow);
        var lines = new List<string>();
        foreach (var item in eligible.OrderBy(x => x.NormalizedTime ?? x.EndAt ?? x.FetchedAt).ThenBy(x => x.Title, StringComparer.Ordinal))
        {
            if (item.TimePrecision == TimePrecision.DateOnly && item.ItemType is "EVENT" or "GACHA"
                && SourceDate(item.SourceTime) is { } dateOnlyStart
                && SourceDate(item.EndExpression) is { } dateOnlyEnd)
            {
                if (dateOnlyStart == reportDate || dateOnlyStart == reportDate.AddDays(1))
                {
                    var dateOnlyStartLine = FormatStart(item, reportDate, null);
                    if (dateOnlyStartLine is not null) lines.Add(dateOnlyStartLine);
                }
                if (dateOnlyEnd == reportDate || dateOnlyEnd == reportDate.AddDays(1))
                    lines.Add(FormatDateOnlyEnd(item, reportDate, dateOnlyEnd));
                continue;
            }
            if (item.ItemType == "VIDEO" && IsOnDate(item.NormalizedTime, reportDate))
            {
                lines.Add($"-{GameName(item.GameCode)} 发布视频【{item.Title}】");
                continue;
            }

            if (item.ItemType == "PREVIEW_NOTICE" && IsOnDate(item.NormalizedTime, reportDate))
            {
                lines.Add($"-{GameName(item.GameCode)} 发布前瞻预告【{item.Title}】");
                continue;
            }

            if (item.ItemType == "PREVIEW_LIVE")
            {
                var live = FormatStart(item, reportDate, "前瞻");
                if (live is not null) lines.Add(live);
                continue;
            }

            var start = FormatStart(item, reportDate, null);
            if (start is not null) lines.Add(start);
            var end = FormatEnd(item, reportDate, localNow);
            if (end is not null) lines.Add(end);
        }
        return string.Join(Environment.NewLine, lines.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// Formats the formal V3 game sources. Manual/imported records are already confirmed by their
    /// workflow; automatic timeline records retain the existing confirmation and verification gate.
    /// </summary>
    public static string FormatV3Games(
        IEnumerable<ManualEventEntity> events,
        IEnumerable<BannerEntity> banners,
        IEnumerable<EndgameReportOccurrence> endgameOccurrences,
        IEnumerable<TimelineItem> automaticTimeline,
        DateOnly reportDate,
        DateTimeOffset? now = null)
    {
        var localNow = ToShanghai(now ?? DateTimeOffset.UtcNow);
        var lines = new List<string>();

        foreach (var item in events
                     .Where(IsFormal)
                     .OrderBy(x => x.StartAt)
                     .ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var start = FormatManualStart(item.Game, "活动", item.Name, item.StartAt, reportDate, exact: true);
            if (start is not null) lines.Add(start);
            var end = FormatManualEnd(item.Game, "活动", item.Name, item.EndAt, reportDate, localNow, exact: true);
            if (end is not null) lines.Add(end);
        }

        foreach (var banner in banners
                     .Where(IsFormal)
                     .OrderBy(x => x.StartAt)
                     .ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var characters = string.Join("、", banner.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name));
            var displayName = string.IsNullOrWhiteSpace(characters) ? banner.Name : $"{banner.Name}（{characters}）";
            var label = string.IsNullOrWhiteSpace(banner.CustomType) ? banner.Type : banner.CustomType;
            var start = FormatManualStart(banner.Game, label, displayName, banner.StartAt, reportDate, exact: true, includeRemaining: banner.EndAt - localNow);
            if (start is not null) lines.Add(start);
            var end = FormatManualEnd(banner.Game, label, displayName, banner.EndAt, reportDate, localNow, exact: true);
            if (end is not null) lines.Add(end);
        }

        foreach (var occurrence in endgameOccurrences
                     .OrderBy(x => x.StartAt)
                     .ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var startDate = DateOnly.FromDateTime(ToShanghai(occurrence.StartAt).Date);
            if (startDate != reportDate && startDate != reportDate.AddDays(1)) continue;
            var day = startDate == reportDate ? "今日" : "明日";
            var time = occurrence.IsDateOnly ? string.Empty : ToShanghai(occurrence.StartAt).ToString("HH:mm");
            lines.Add($"-{GameName(occurrence.Game)} 周期玩法「{occurrence.Name}」{day}{time}刷新");
        }

        var automatic = FormatGames(automaticTimeline, reportDate, now);
        if (!string.IsNullOrWhiteSpace(automatic)) lines.Add(automatic);
        return string.Join(Environment.NewLine, lines.Distinct(StringComparer.Ordinal));
    }

    public static string FormatBgi(IEnumerable<GitCommitRecord> commits)
    {
        var selected = commits.Where(x => x.SelectedForReport).ToList();
        var main = selected.Where(x => x.Repository.Contains("better-genshin-impact", StringComparison.OrdinalIgnoreCase));
        var scripts = selected.Where(x => x.Repository.Contains("bettergi-scripts-list", StringComparison.OrdinalIgnoreCase));
        var other = selected.Except(main.Concat(scripts));
        var sections = new List<string> { "BGI更新预告（所有改动随下一版本同步）" };
        AddCommits(sections, main);
        sections.Add(string.Empty);
        sections.Add("BGI仓库更新");
        AddCommits(sections, scripts);
        AddCommits(sections, other);
        return string.Join(Environment.NewLine, sections).Trim();
    }

    public static string FormatArtworks(IEnumerable<ArtworkEntity> artworks)
        => string.Join(Environment.NewLine, artworks.Where(x => x.ReviewStatus == ReviewStatus.Confirmed && x.SelectedForReport)
            .OrderBy(x => x.PublishedAt)
            .Select(x => string.Join(" ", new[]
            {
                x.CharacterName.Trim(),
                FormatArtworkFranchise(x.FranchiseName),
                "来源：" + FormatArtworkPlatform(x.Platform)
            }.Where(x => !string.IsNullOrWhiteSpace(x)))));

    private static string FormatArtworkFranchise(string franchise) => franchise.Trim().ToUpperInvariant() switch
    {
        "GENSHIN" => "原神",
        "STARRAIL" => "崩坏：星穹铁道",
        "NTE" => "异环",
        "HI3" => "崩坏3",
        "ZZZ" => "绝区零",
        "WUTHERINGWAVES" or "WUWA" => "鸣潮",
        _ => franchise.Trim()
    };

    private static string FormatArtworkPlatform(string platform) => platform.Trim().ToUpperInvariant() switch
    {
        "PIXIV" => "pixiv",
        _ => platform.Trim()
    };

    private static void AddCommits(List<string> target, IEnumerable<GitCommitRecord> commits)
    {
        foreach (var commit in commits.OrderBy(x => x.CommitterDate ?? x.AuthorDate ?? x.FetchedAt))
            target.Add($"-{commit.Subject} ({commit.Sha[..Math.Min(7, commit.Sha.Length)]})");
    }

    private static string? FormatStart(TimelineItem item, DateOnly date, string? prefix)
    {
        var startDate = item.NormalizedTime is { } normalized
            ? DateOnly.FromDateTime(ToShanghai(normalized).Date)
            : SourceDate(item.SourceTime);
        if (startDate is null || (startDate != date && startDate != date.AddDays(1))) return null;
        var local = item.NormalizedTime is { } value ? ToShanghai(value) : (DateTimeOffset?)null;
        var day = startDate == date ? "今日" : "明日";
        var time = local is { } exact && item.TimePrecision == TimePrecision.Exact ? exact.ToString("HH:mm") : null;
        var label = prefix ?? ItemLabel(item.ItemType);
        if (item.ItemType == "GACHA" && HasStructuredPool(item)) label = GachaLabel(item);
        var title = $"{label}「{item.Title}」";
        return $"-{GameName(item.GameCode)} {title}{day}{time}开始";
    }

    private static string? FormatEnd(TimelineItem item, DateOnly date, DateTimeOffset now)
    {
        if (item.EndAt is not { } endAt) return null;
        var local = ToShanghai(endAt);
        var endDate = DateOnly.FromDateTime(local.Date);
        if (endDate != date && endDate != date.AddDays(1)) return null;
        var day = endDate == date ? "今日" : "明日";
        var time = item.TimePrecision == TimePrecision.Exact || endAt != item.NormalizedTime ? local.ToString("HH:mm") : null;
        var remaining = endAt - now.ToUniversalTime();
        var remainingText = remaining > TimeSpan.Zero ? $"剩余 {Duration(remaining)}，" : string.Empty;
        var label = item.ItemType == "GACHA" && HasStructuredPool(item) ? GachaLabel(item) : ItemLabel(item.ItemType);
        return $"-{GameName(item.GameCode)} {label}「{item.Title}」{remainingText}将于{day}{time}结束";
    }

    private static string FormatDateOnlyEnd(TimelineItem item, DateOnly reportDate, DateOnly endDate)
    {
        var day = endDate == reportDate ? "今日" : "明日";
        var label = item.ItemType == "GACHA" && HasStructuredPool(item) ? GachaLabel(item) : ItemLabel(item.ItemType);
        return $"-{GameName(item.GameCode)} {label}「{item.Title}」将于{day}结束";
    }

    private static bool IsFormal(ManualEventEntity item)
        => !item.Archived && item.UserConfirmed && item.Origin is DataOrigin.Manual or DataOrigin.Imported;

    private static bool IsFormal(BannerEntity item)
        => !item.Archived && item.UserConfirmed && item.Origin is DataOrigin.Manual or DataOrigin.Imported;

    private static string? FormatManualStart(string game, string label, string name, DateTimeOffset startAt, DateOnly date, bool exact, TimeSpan? includeRemaining = null)
    {
        var local = ToShanghai(startAt);
        var startDate = DateOnly.FromDateTime(local.Date);
        if (startDate != date && startDate != date.AddDays(1)) return null;
        var day = startDate == date ? "今日" : "明日";
        var remaining = includeRemaining is { } duration && duration > TimeSpan.Zero ? $"，剩余 {Duration(duration)}" : string.Empty;
        return $"-{GameName(game)} {label}「{name}」{day}{(exact ? local.ToString("HH:mm") : string.Empty)}开始{remaining}";
    }

    private static string? FormatManualEnd(string game, string label, string name, DateTimeOffset endAt, DateOnly date, DateTimeOffset now, bool exact)
    {
        var local = ToShanghai(endAt);
        var endDate = DateOnly.FromDateTime(local.Date);
        if (endDate != date && endDate != date.AddDays(1)) return null;
        var day = endDate == date ? "今日" : "明日";
        var remaining = endAt - now.ToUniversalTime();
        var remainingText = remaining > TimeSpan.Zero ? $"剩余 {Duration(remaining)}，" : string.Empty;
        return $"-{GameName(game)} {label}「{name}」{remainingText}将于{day}{(exact ? local.ToString("HH:mm") : string.Empty)}结束";
    }

    private static bool IsOnDate(DateTimeOffset? value, DateOnly date)
        => value is { } instant && DateOnly.FromDateTime(ToShanghai(instant).Date) == date;

    private static DateOnly? SourceDate(string? sourceTime)
    {
        if (string.IsNullOrWhiteSpace(sourceTime)) return null;
        var value = sourceTime.Trim();
        if (value.Length >= 10 && DateOnly.TryParse(value[..10], out var date)) return date;
        return DateOnly.TryParse(value, out date) ? date : null;
    }

    private static string ItemLabel(string itemType) => itemType switch
    {
        "EVENT" => "活动",
        "GACHA" => "卡池",
        "ENDGAME" => "周期挑战",
        "PREVIEW_NOTICE" => "前瞻预告",
        "PREVIEW_LIVE" => "前瞻",
        _ => "其他内容"
    };

    private static bool HasStructuredPool(TimelineItem item)
        => !string.IsNullOrWhiteSpace(item.GachaPoolKind) && !string.IsNullOrWhiteSpace(item.GachaPoolPhase)
            && item.GachaPoolKind != "UNKNOWN" && item.GachaPoolPhase != "UNKNOWN";

    private static string GachaLabel(TimelineItem item)
    {
        var phase = item.GachaPoolPhase switch
        {
            "FIRST_HALF" => "\u4e0a\u534a",
            "SECOND_HALF" => "\u4e0b\u534a",
            _ => "\u5f85\u786e\u8ba4"
        };
        return phase + "\u5361\u6c60";
    }

    private static string GameName(string gameCode) => gameCode switch
    {
        "" => string.Empty,
        "GENSHIN" => "原神",
        "STARRAIL" => "崩铁",
        "NTE" => "异环",
        "HI3" => "崩坏三",
        _ => "其他游戏"
    };

    private static DateTimeOffset ToShanghai(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Shanghai);

    private static string Duration(TimeSpan duration)
    {
        var totalHours = Math.Max(0, (int)Math.Floor(duration.TotalHours));
        return $"{totalHours / 24}天{totalHours % 24}小时";
    }
}
