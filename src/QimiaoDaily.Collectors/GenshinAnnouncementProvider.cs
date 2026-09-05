using System.Net.Http.Json;
using System.Text.Json;
using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors;

public sealed record CollectedEvidence(string Provider, string SourceType, string SourceUrl, string SourceText, DateTimeOffset FetchedAt, string? PageTitle = null, DateTimeOffset? PublishedAt = null, string? OriginalTimezone = null, DateTimeOffset? NormalizedTime = null);

public sealed record GameCandidate(
    string ExternalId,
    string GameCode,
    string ItemType,
    string Title,
    string? SourceTime,
    string? SourceTimezone,
    DateTimeOffset? NormalizedTime,
    IReadOnlyList<CollectedEvidence> Evidence,
    DateTimeOffset? EndAt = null,
    TimePrecision StartTimePrecision = TimePrecision.Exact,
    TimePrecision EndTimePrecision = TimePrecision.Exact,
    string? StartTimeSource = null,
    string? EndTimeSource = null,
    string? StartExpression = null,
    string? EndExpression = null,
    string? StartTimeEvidenceKey = null,
    string? EndTimeEvidenceKey = null,
    string? GachaPoolKind = null,
    string? GachaPoolPhase = null,
    string? GachaGroupKey = null)
{
    public int SourceCandidateCount { get; init; }
    public int SourceRejectedCount { get; init; }
    public IReadOnlyDictionary<string, int> SourceRejectionReasons { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static GameCandidate Relative(
        string externalId,
        string gameCode,
        string itemType,
        string title,
        string sourceTime,
        string sourceTimezone,
        string startExpression,
        string endExpression,
        string startTimeEvidenceKey,
        string? endTimeEvidenceKey = null) => new(
            externalId,
            gameCode,
            itemType,
            title,
            sourceTime,
            sourceTimezone,
            null,
            [],
            null,
            TimePrecision.Relative,
            TimePrecision.Relative,
            "relative-expression",
            "relative-expression",
            startExpression,
            endExpression,
            startTimeEvidenceKey,
            endTimeEvidenceKey);
}

public sealed class GenshinAnnouncementProvider(HttpClient client)
{
    public const string ListUrl = "https://hk4e-api.mihoyo.com/common/hk4e_cn/announcement/api/getAnnList?game=hk4e&game_biz=hk4e_cn&lang=zh-cn&bundle_id=hk4e_cn&platform=pc&region=cn_gf01&level=55&uid=100000000";
    public const string ContentUrl = "https://hk4e-api.mihoyo.com/common/hk4e_cn/announcement/api/getAnnContent?game=hk4e&game_biz=hk4e_cn&lang=zh-cn&bundle_id=hk4e_cn&platform=pc&region=cn_gf01&level=55&uid=100000000";
    private static readonly string[] Ignored = ["\u95ee\u5377", "\u9632\u6c89\u8ff7", "\u7c73\u6e38\u793e", "\u5468\u8fb9", "\u9884\u4e0b\u8f7d", "\u7248\u672c\u66f4\u65b0\u8bf4\u660e", "\u66f4\u65b0\u4fee\u590d", "\u793e\u533a", "\u8c03\u7814"];

    public async Task<IReadOnlyList<GameCandidate>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var fetched = DateTimeOffset.UtcNow;
        using var listResponse = await client.GetAsync(ListUrl, cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        using var contentResponse = await client.GetAsync(ContentUrl, cancellationToken);
        contentResponse.EnsureSuccessStatusCode();
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        using var content = JsonDocument.Parse(await contentResponse.Content.ReadAsStringAsync(cancellationToken));
        var details = Objects(content.RootElement).Where(x => x.TryGetProperty("ann_id", out _)).ToDictionary(x => Id(x), x => Text(x, "content"));
        var candidates = new List<GameCandidate>();
        var sourceItems = Objects(list.RootElement).Where(x => x.TryGetProperty("ann_id", out _)).GroupBy(Id).Select(x => x.First()).ToArray();
        var rejected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void Reject(string reason) => rejected[reason] = rejected.GetValueOrDefault(reason) + 1;
        foreach (var item in sourceItems)
        {
            var title = Text(item, "title");
            var body = details.GetValueOrDefault(Id(item), string.Empty);
            var type = item.TryGetProperty("type", out var typeElement) ? typeElement.GetInt32() : 0;
            var isGacha = title.Contains("\u7948\u613f") || title.Contains("\u6982\u7387UP");
            var isEndgame = title.Contains("\u6df1\u5883\u87ba\u65cb") || title.Contains("\u5e7b\u60f3\u771f\u5883\u5267\u8bd7") || title.Contains("\u5e7d\u5883\u5371\u6218");
            var isActivity = type == 1 && !string.IsNullOrWhiteSpace(Text(item, "end_time"));
            if (string.IsNullOrWhiteSpace(title)) { Reject("missing_title"); continue; }
            if (Ignored.Any(title.Contains)) { Reject("ignored_rule"); continue; }
            if (type == 1 && !isGacha && !isActivity && string.IsNullOrWhiteSpace(Text(item, "end_time"))) { Reject("missing_activity_end"); continue; }
            if (!isGacha && !isActivity && !isEndgame) { Reject("unsupported_category"); continue; }
            var start = Text(item, "start_time");
            var end = Text(item, "end_time");
            var id = Id(item);
            var window = AnnouncementTimeParser.Parse(body, start, end, "GENSHIN");
            var gacha = isGacha ? GachaClassification.Classify("GENSHIN", title, body) : new GachaClassificationResult("UNKNOWN", "UNKNOWN", null, false);
            var evidence = new[]
            {
                new CollectedEvidence("GenshinOfficial", "announcement-list", $"{ListUrl}&ann_id={id}", title, fetched),
                new CollectedEvidence("GenshinOfficial", "announcement-content", $"{ContentUrl}&ann_id={id}", string.IsNullOrWhiteSpace(body) ? title : body, fetched, NormalizedTime: window.Start)
            };
            candidates.Add(new GameCandidate(id, "GENSHIN", isGacha ? "GACHA" : isEndgame ? "ENDGAME" : "EVENT", title, start, "Asia/Shanghai", window.Start, evidence, window.End,
                window.Precision, window.End is null ? TimePrecision.Relative : window.Precision == TimePrecision.Relative ? TimePrecision.Exact : window.Precision,
                window.StartSource, window.EndSource, window.StartExpression, window.EndExpression,
                $"{id}:start", $"{id}:end", isGacha ? gacha.PoolKind : null, isGacha ? gacha.PoolPhase : null, isGacha ? gacha.GroupKey : null));
        }
        var rejectedCount = rejected.Values.Sum();
        return candidates.Select(candidate => candidate with
        {
            SourceCandidateCount = sourceItems.Length,
            SourceRejectedCount = rejectedCount,
            SourceRejectionReasons = new Dictionary<string, int>(rejected, StringComparer.OrdinalIgnoreCase)
        }).ToArray();
    }

    private static string Id(JsonElement item) => item.GetProperty("ann_id").ToString();
    private static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    private static IEnumerable<JsonElement> Objects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object) { yield return element; foreach (var property in element.EnumerateObject()) foreach (var child in Objects(property.Value)) yield return child; }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) foreach (var nested in Objects(child)) yield return nested;
    }
}
