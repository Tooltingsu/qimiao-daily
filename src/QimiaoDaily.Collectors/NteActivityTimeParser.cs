namespace QimiaoDaily.Collectors;

public static class NteActivityTimeParser
{
    public static ParsedTimeWindow ParseForTest(string body, string? publishedText)
    {
        var parsed = AnnouncementTimeParser.Parse(body, null, null, "NTE");
        return parsed with { PublishedAt = AnnouncementTimeParser.ParseSingleDateForTest(publishedText) };
    }

    public static ParsedTimeWindow Parse(string? body, string? publishedText)
        => ParseForTest(body ?? string.Empty, publishedText);
}
