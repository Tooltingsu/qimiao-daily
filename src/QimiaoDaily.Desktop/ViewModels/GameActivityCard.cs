using QimiaoDaily.Desktop.Localization;
using QimiaoDaily.Core;
using System.Collections.Generic;

namespace QimiaoDaily.Desktop.ViewModels;

public sealed record GameActivityCard(Guid Id, string Game, string Type, string Title, string TimeText, string RemainingText, string EvidenceUrl, string EvidenceSummary, string Verification,
    DateTimeOffset? StartAt = null, DateTimeOffset? EndAt = null, string? GachaPoolKind = null, string? GachaPoolPhase = null)
{
    private static readonly string[] ProviderKeys =
    [
        "GenshinOfficial", "StarRailOfficial", "NteOfficialWebsite", "NteBilibiliOfficial",
        "Honkai3OfficialCharacterList", "Hi3BiligameBirthday", "Hi3BaiduBirthday", "Hi3MoegirlBirthday",
        "BirthdayHoYoWiki", "NteFandomBirthday", "OfficialYoutuberss", "OfficialYoutuberssGenshin",
        "OfficialYoutuberssStarRail", "NteNevernessGgBirthday", "Pixiv", "BgiGithub"
    ];

    public string DisplayGame => DisplayNameMapper.Game(Game);
    public string DisplayType => string.Join(" · ", Type.Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(part =>
        DisplayNameMapper.ItemType(part) == part ? DisplayNameMapper.Change(part) : DisplayNameMapper.ItemType(part)));
    public string DisplayVerification => DisplayNameMapper.Verification(Verification);
    public string DisplayGachaPool => string.Equals(Type.Split('·', StringSplitOptions.TrimEntries)[0], "GACHA", StringComparison.OrdinalIgnoreCase)
        ? $"{DisplayNameMapper.GachaPoolKind(GachaPoolKind)} · {DisplayNameMapper.GachaPoolPhase(GachaPoolPhase)}" : string.Empty;
    public string StoredCategory => Type.Split('·', StringSplitOptions.TrimEntries)[0] switch
    {
        "EVENT" => "活动",
        "GACHA" => "卡池",
        "ENDGAME" => "深渊",
        "VIDEO" => "视频",
        "PREVIEW_NOTICE" or "PREVIEW_LIVE" => "前瞻",
        _ => "其他"
    };

    public override string ToString() => $"{DisplayGame} · {DisplayType} · {Title} · {DisplayVerification}";

    public string DisplayEvidenceSummary
    {
        get
        {
            var text = EvidenceSummary
                .Replace("Change:", "变更：", StringComparison.OrdinalIgnoreCase)
                .Replace("Verification:", "核验：", StringComparison.OrdinalIgnoreCase)
                .Replace("No evidence", "暂无证据", StringComparison.OrdinalIgnoreCase)
                .Replace("NEW", "新增", StringComparison.OrdinalIgnoreCase)
                .Replace("TIME_CHANGED", "时间变更", StringComparison.OrdinalIgnoreCase)
                .Replace("CONTENT_CHANGED", "内容变更", StringComparison.OrdinalIgnoreCase)
                .Replace("SOURCE_CHANGED", "来源变更", StringComparison.OrdinalIgnoreCase)
                .Replace("CONFLICT", "来源冲突", StringComparison.OrdinalIgnoreCase)
                .Replace("NONE", "无变更", StringComparison.OrdinalIgnoreCase)
                .Replace("VerifiedOfficial", "官方已核验", StringComparison.OrdinalIgnoreCase)
                .Replace("VerifiedMultiSource", "多源已核验", StringComparison.OrdinalIgnoreCase)
                .Replace("Unverified", "待核验", StringComparison.OrdinalIgnoreCase);
            foreach (var provider in ProviderKeys)
                text = text.Replace(provider, DisplayNameMapper.Provider(provider), StringComparison.OrdinalIgnoreCase);
            return text;
        }
    }
}

public static class ActivityRemainingRefresher
{
    public static void Refresh(IList<GameActivityCard> cards, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(zone);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            cards[index] = card with { RemainingText = TimeDisplay.Format(card.StartAt, card.EndAt, now, zone) };
        }
    }
}

public sealed record EvidenceDrawerCard(Guid Id, string Game, string Type, string Title, string Provider, string SourceUrl, string SourceText,
    string OriginalTime, string NormalizedTime, string PublishedAt, string FetchedAt, string Timezone, string ParserVersion,
    string Verification, string History)
{
    public string DisplayGame => DisplayNameMapper.Game(Game);
    public string DisplayType => DisplayNameMapper.ItemType(Type);
    public string DisplayVerification => DisplayNameMapper.Verification(Verification);
};
