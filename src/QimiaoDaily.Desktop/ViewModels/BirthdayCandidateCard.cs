using QimiaoDaily.Core;
using QimiaoDaily.Desktop.Localization;

namespace QimiaoDaily.Desktop.ViewModels;

public sealed class BirthdayCandidateCard
{
    public Guid Id { get; init; }
    public string Character { get; init; } = string.Empty;
    public string Franchise { get; init; } = string.Empty;
    public string DateText { get; init; } = "生日未知";
    public string StatusText { get; init; } = string.Empty;
    public string EnabledText { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public bool CanEnable { get; init; }
    public string ActionText => Enabled ? "停用" : "启用日报";
    public string VerificationStatus => Verification.ToString();
    public VerificationStatus Verification { get; init; }
    public string DisplayFranchise => DisplayNameMapper.Game(Franchise);
}
