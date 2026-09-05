using CommunityToolkit.Mvvm.ComponentModel;

namespace QimiaoDaily.Desktop.ViewModels;
public sealed class BgiCommitCard
{
    public Guid Id { get; init; }
    public string Repository { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Sha { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public string PullRequestText { get; init; } = "无 PR";
    public string Url { get; init; } = string.Empty;
}
public sealed partial class ReportSectionCard : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName => Key switch
    {
        "calendar" => "节日与纪念日",
        "games" => "游戏活动预览",
        "bgi" => "BGI 更新",
        "artwork" => "美图分享",
        _ => "其他日报内容"
    };
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _dirty;
    [ObservableProperty] private bool _manualOverride;
    public bool IsDeleted { get; init; }
}
