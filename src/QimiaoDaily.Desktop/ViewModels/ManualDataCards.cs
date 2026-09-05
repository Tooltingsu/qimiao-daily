using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.ViewModels;

public sealed record ManualEventCard(Guid Id, string Game, string Name, string Start, string End, string Notes, bool Archived)
{
    public string Status => Archived ? "已归档" : "正式数据";
}

public sealed record ManualBannerCard(Guid Id, string Game, string Name, string Type, string Characters, string Start, string End, string Notes, bool Archived)
{
    public string Status => Archived ? "已归档" : "正式数据";
}

public sealed record ManualVersionCard(Guid Id, string Game, string VersionNumber, string VersionName, string Start, string End, string Notes, bool Archived)
{
    public string Status => Archived ? "已归档" : "正式数据";
}

public sealed record AnniversaryCard(Guid Id, string Title, string StartedOn, string Notes, bool Enabled)
{
    public string Status => Enabled ? "已启用" : "已停用";
    public string ActionText => Enabled ? "停用" : "启用";
}

public sealed record EndgameRuleCard(Guid Id, string Game, string Name, string RuleKind, string ConfigurationJson, bool Enabled, string AnchorText, bool CanReanchor = true)
{
    public string ReanchorHint => CanReanchor
        ? "可修改该规则的计算起点。"
        : "版本相关规则不能单独锚定，请在版本管理中录入版本时间。";
}

public sealed record EndgameOccurrenceCard(Guid Id, Guid RuleId, string RuleName, string DisplayStart, string DisplayEnd, bool IsOverride);

public sealed record ImportPreviewCard(string SelectionKey, string RecordType, string Change, string NaturalKey, bool IsSelectable)
{
    public bool IsSelected { get; set; } = IsSelectable;
}

public sealed partial class BannerCharacterEditor : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _name = string.Empty;
}

internal static class ManualDataCardMapper
{
    public static ManualEventCard ToCard(ManualEventEntity value) => new(value.Id, value.Game, value.Name, Format(value.StartAt), Format(value.EndAt), value.Notes, value.Archived);
    public static ManualBannerCard ToCard(BannerEntity value) => new(value.Id, value.Game, value.Name, string.IsNullOrWhiteSpace(value.CustomType) ? value.Type : value.CustomType!, string.Join("、", value.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name)), Format(value.StartAt), Format(value.EndAt), value.Notes, value.Archived);
    public static ManualVersionCard ToCard(GameVersionEntity value) => new(value.Id, value.Game, value.VersionNumber, value.VersionName, Format(value.StartAt), Format(value.EndAt), value.Notes, value.Archived);
    public static AnniversaryCard ToCard(AnniversaryEntity value) => new(value.Id, value.Title, value.StartedOn.ToString("yyyy-MM-dd"), value.Notes, value.Enabled);

    public static EndgameOccurrenceCard ToCard(EndgameOccurrenceEntity value, string ruleName, bool dateOnly)
        => new(value.Id, value.RuleId, ruleName,
            dateOnly ? FormatDate(value.OccurrenceDate) : Format(value.StartAt),
            dateOnly ? FormatDate(value.OccurrenceDate) : Format(value.EndAt), value.IsOverride);

    private static string Format(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm");
    private static string FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "日期待确认";
}
