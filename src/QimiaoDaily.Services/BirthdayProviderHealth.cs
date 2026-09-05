using QimiaoDaily.Collectors;

namespace QimiaoDaily.Services;

public sealed record BirthdayProviderHealthSummary(string Status, int Total, int Known, int Unknown, string Message);

public static class BirthdayProviderHealth
{
    public static BirthdayProviderHealthSummary Summarize(IEnumerable<BirthdaySource> candidates)
    {
        var rows = candidates?.ToArray() ?? throw new ArgumentNullException(nameof(candidates));
        var known = rows.Count(x => x.Month is >= 1 and <= 12 && x.Day >= 1 && x.Day <= DateTime.DaysInMonth(2024, x.Month));
        var unknown = rows.Length - known;
        var status = rows.Length == 0 ? "FAILED" : known == 0 ? "FAILED" : unknown == 0 ? "HEALTHY" : "PARTIAL";
        var message = $"生日候选有日期 {known}/{rows.Length}，未知 {unknown}。";
        return new(status, rows.Length, known, unknown, message);
    }
}
