using System.Text.Json;
using QimiaoDaily.Core;

namespace QimiaoDaily.Collectors;

public sealed class StarRailAnnouncementProvider(HttpClient client)
{
    public const string ListUrl = "https://hkrpg-api.mihoyo.com/common/hkrpg_cn/announcement/api/getAnnList?game=hkrpg&game_biz=hkrpg_cn&lang=zh-cn&auth_appid=announcement&authkey_ver=1&bundle_id=hkrpg_cn&channel_id=1&level=65&platform=pc&region=prod_gf_cn&sdk_presentation_style=fullscreen&sdk_screen_transparent=true&sign_type=2&uid=100000000";
    public const string ContentUrl = "https://hkrpg-api-static.mihoyo.com/common/hkrpg_cn/announcement/api/getAnnContent?game=hkrpg&game_biz=hkrpg_cn&lang=zh-cn&bundle_id=hkrpg_cn&platform=pc&region=prod_gf_cn&level=65&channel_id=1";
    private static readonly string[] Ignored = ["\u95ee\u5377", "\u9632\u6c89\u8ff7", "\u793e\u7fa4", "\u793e\u533a", "\u4f18\u5316", "\u66f4\u65b0\u8bf4\u660e", "\u5546\u5e97", "\u6d4b\u8bd5\u62db\u52df"];

    public async Task<IReadOnlyList<GameCandidate>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var fetched = DateTimeOffset.UtcNow;
        using var listResponse = await client.GetAsync(ListUrl, cancellationToken); listResponse.EnsureSuccessStatusCode();
        using var contentResponse = await client.GetAsync(ContentUrl, cancellationToken); contentResponse.EnsureSuccessStatusCode();
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        using var content = JsonDocument.Parse(await contentResponse.Content.ReadAsStringAsync(cancellationToken));
        var details = Objects(content.RootElement).Where(x => x.TryGetProperty("ann_id", out _)).GroupBy(Id).ToDictionary(x => x.Key, x => Text(x.First(), "content"));
        var result = new List<GameCandidate>();
        var sourceItems = Objects(list.RootElement).Where(x => x.TryGetProperty("ann_id", out _)).GroupBy(Id).Select(x => x.First()).ToArray();
        var rejected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void Reject(string reason) => rejected[reason] = rejected.GetValueOrDefault(reason) + 1;
        foreach (var item in sourceItems)
        {
            var title = Text(item, "title"); var kind = item.TryGetProperty("type", out var t) ? t.GetInt32() : 0;
            var start = Text(item, "start_time"); var end = Text(item, "end_time");
            var isGacha = title.Contains("\u6d3b\u52a8\u8dc3\u8fc1") || title.Contains("\u5149\u9525\u6d3b\u52a8\u8dc3\u8fc1");
            var isEndgame = title.Contains("\u6df7\u6c8c\u56de\u5fc6") || title.Contains("\u865a\u6784\u53d9\u4e8b") || title.Contains("\u672b\u65e5\u5e7b\u5f71") || title.Contains("\u5f02\u76f8\u4ef2\u88c1");
            var isCandidate = (kind is 3 or 4) && !string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end);
            if (string.IsNullOrWhiteSpace(title)) { Reject("missing_title"); continue; }
            if (Ignored.Any(title.Contains)) { Reject("ignored_rule"); continue; }
            if (!isCandidate && !isEndgame)
            {
                Reject(kind is 3 or 4 ? "missing_source_time" : "unsupported_category");
                continue;
            }
            var id = Id(item);
            var body = details.GetValueOrDefault(id, title);
            var window = AnnouncementTimeParser.Parse(body, start, end, "STARRAIL");
            var gacha = isGacha ? GachaClassification.Classify("STARRAIL", title, body) : new GachaClassificationResult("UNKNOWN", "UNKNOWN", null, false);
            result.Add(new GameCandidate(id, "STARRAIL", isGacha ? "GACHA" : isEndgame ? "ENDGAME" : "EVENT", title, start, "Asia/Shanghai", window.Start,
                [new CollectedEvidence("StarRailOfficial", "announcement-list", $"{ListUrl}&ann_id={id}", title, fetched), new CollectedEvidence("StarRailOfficial", "announcement-content", $"{ContentUrl}&ann_id={id}", string.IsNullOrWhiteSpace(body) ? title : body, fetched, NormalizedTime: window.Start)], window.End,
                window.Precision, window.End is null ? TimePrecision.Relative : window.Precision == TimePrecision.Relative ? TimePrecision.Exact : window.Precision,
                window.StartSource, window.EndSource, window.StartExpression, window.EndExpression,
                $"{id}:start", $"{id}:end", isGacha ? gacha.PoolKind : null, isGacha ? gacha.PoolPhase : null, isGacha ? gacha.GroupKey : null));
        }
        var rejectedCount = rejected.Values.Sum();
        return result.Select(candidate => candidate with
        {
            SourceCandidateCount = sourceItems.Length,
            SourceRejectedCount = rejectedCount,
            SourceRejectionReasons = new Dictionary<string, int>(rejected, StringComparer.OrdinalIgnoreCase)
        }).ToArray();
    }
    private static string Id(JsonElement item) => item.GetProperty("ann_id").ToString();
    private static string Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    private static IEnumerable<JsonElement> Objects(JsonElement e){if(e.ValueKind==JsonValueKind.Object){yield return e;foreach(var p in e.EnumerateObject())foreach(var x in Objects(p.Value))yield return x;}else if(e.ValueKind==JsonValueKind.Array)foreach(var c in e.EnumerateArray())foreach(var x in Objects(c))yield return x;}
}
