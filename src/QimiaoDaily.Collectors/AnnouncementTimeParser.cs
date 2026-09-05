using System.Globalization;
using System.Text.RegularExpressions;
using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors;

public sealed record ParsedTimeWindow(
    DateTimeOffset? Start,
    DateTimeOffset? End,
    TimePrecision Precision,
    string? StartSource,
    string? EndSource,
    string? StartExpression,
    string? EndExpression,
    DateTimeOffset? PublishedAt = null);

public static class AnnouncementTimeParser
{
    private static readonly Regex LabelPattern = new(
        @"(?<label>活动时间|开放时间|任务开放时间|祈愿时间|跃迁时间|开启时间|活动跃迁)\s*[:：]?\s*(?<range>[^<>\r\n]{4,160})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DatePattern = new(
        @"(?:(?<year>20\d{2})\s*[年/\-.])?(?<month>\d{1,2})\s*(?:月|/)\s*(?<day>\d{1,2})\s*(?:日)?(?:\s*(?<hour>\d{1,2})(?:[:：点](?<minute>\d{1,2}))?)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RelativePattern = new(
        @"(?<expression>[^\-—~]{0,30}(?:更新后|开启后|维护后))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ParsedTimeWindow ParseForTest(string body, string? listStart, string? listEnd, string gameCode)
        => Parse(body, listStart, listEnd, gameCode);

    public static DateTimeOffset? ParseSingleDateForTest(string? value, int? fallbackYear = null)
        => ParseSingle(value, fallbackYear);

    public static ParsedTimeWindow Parse(string? body, string? listStart, string? listEnd, string gameCode)
    {
        var normalizedBody = Normalize(body);
        var labeled = LabelPattern.Match(normalizedBody);
        var sourceText = labeled.Success ? labeled.Groups["range"].Value : normalizedBody;
        var contextYear = ParseSingle(listStart)?.Year ?? ParseSingle(listEnd)?.Year ??
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").Year;
        var labeledDates = labeled.Success
            ? DatePattern.Matches(sourceText).Cast<Match>().ToList()
            : [];
        var relative = labeled.Success
            ? RelativePattern.Match(sourceText)
            : RelativePattern.Match(normalizedBody);
        // A version note after an explicit two-date interval is explanatory text,
        // while a relative phrase before the first date defines the start.
        if (labeled.Success && labeledDates.Count >= 2 && relative.Success && relative.Index > labeledDates[0].Index)
            relative = Match.Empty;

        if (relative.Success)
        {
            var dates = labeled.Success
                ? labeledDates
                : DatePattern.Matches(sourceText).Cast<Match>().ToList();
            var endMatch = dates.LastOrDefault();
            var end = endMatch is null ? null : ParseMatch(endMatch, contextYear);
            return new(
                null,
                end,
                TimePrecision.Relative,
                "relative-expression",
                end is null ? null : labeled.Success ? "activity-body" : "announcement-list",
                relative.Groups["expression"].Value.Trim(),
                endMatch?.Value.Trim(),
                null);
        }

        if (labeled.Success)
        {
            var start = labeledDates.Count > 0 ? ParseMatch(labeledDates[0], contextYear) : null;
            var end = labeledDates.Count > 1 ? ParseMatch(labeledDates[^1], start?.Year ?? contextYear) : ParseSingle(listEnd, start?.Year ?? contextYear);
            return new(start, end, start is null ? TimePrecision.DateOnly : TimePrecision.Exact,
                start is null ? null : "activity-body", end is null ? null : "activity-body",
                labeledDates.Count > 0 ? labeledDates[0].Value.Trim() : null, labeledDates.Count > 1 ? labeledDates[^1].Value.Trim() : null);
        }

        var listEndValue = ParseSingle(listEnd, contextYear);
        return new(null, listEndValue, TimePrecision.Relative,
            null, listEndValue is null ? null : "announcement-list",
            null, listEnd);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = Regex.Replace(value, @"<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("\u3000", " ")
            .Replace("至", "-")
            .Replace("到", "-");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static DateTimeOffset? ParseMatch(Match match, int fallbackYear)
    {
        if (!int.TryParse(match.Groups["month"].Value, out var month) || !int.TryParse(match.Groups["day"].Value, out var day)) return null;
        var year = match.Groups["year"].Success && int.TryParse(match.Groups["year"].Value, out var parsedYear) ? parsedYear : fallbackYear;
        // Official announcement feeds use far-future sentinel dates for open-ended
        // notices. Keep the source expression, but do not turn those sentinels into
        // a misleading countdown in the local database.
        if (year > fallbackYear + 2) return null;
        var hour = match.Groups["hour"].Success && int.TryParse(match.Groups["hour"].Value, out var parsedHour) ? parsedHour : 0;
        var minute = match.Groups["minute"].Success && int.TryParse(match.Groups["minute"].Value, out var parsedMinute) ? parsedMinute : 0;
        try { return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8)); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static DateTimeOffset? ParseSingle(string? value, int? fallbackYear = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = Normalize(value);
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            var maxYear = (fallbackYear ?? DateTimeOffset.UtcNow.Year) + 2;
            return exact.Year > maxYear ? null : exact.Offset == TimeSpan.Zero ? exact.ToOffset(TimeSpan.FromHours(8)) : exact;
        }
        var match = DatePattern.Match(text);
        return match.Success ? ParseMatch(match, fallbackYear ?? DateTimeOffset.UtcNow.Year) : null;
    }
}
