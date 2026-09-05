using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Desktop.Localization;
using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    public event Action? BirthdayEditorRequested;
    public event Action? AnniversaryEditorRequested;
    public event Action? ManualEventEditorRequested;
    public event Action? BannerEditorRequested;
    public event Action? VersionEditorRequested;
    public event Action? EndgameReanchorRequested;
    public event Action? EndgameOverrideRequested;
    public event Action? ActivityEditorRequested;
    public event Action? ArtworkEditorRequested;
    public event Action? ImportEditorRequested;
    public event Action<ReportSectionCard>? ReportSectionEditorRequested;
    private readonly string _connectionString;
    private readonly QimiaoDailyPaths _paths = new();
    private DateOnly _calendarDate;
    private readonly List<CalendarDayCard> _allYearCalendarDays = [];
    private readonly DispatcherTimer _remainingTimer;
    private QimiaoImportPreview? _activeImportPreview;

    public ObservableCollection<GameActivityCard> PendingActivities { get; } = [];
    public ObservableCollection<GameActivityCard> ConfirmedActivities { get; } = [];
    public ObservableCollection<GameActivityCard> ArchivedActivities { get; } = [];
    public ObservableCollection<ArtworkCard> PendingArtworks { get; } = [];
    public ObservableCollection<ArtworkCard> ConfirmedArtworks { get; } = [];
    public ObservableCollection<CalendarDayCard> MonthCalendarDays { get; } = [];
    public ObservableCollection<CalendarDayCard> YearCalendarDays { get; } = [];
    public ObservableCollection<CalendarEventCard> CalendarEvents { get; } = [];
    public ObservableCollection<AnniversaryCard> Anniversaries { get; } = [];
    public ObservableCollection<GameActivityCard> StoredGameActivities { get; } = [];
    public ICollectionView StoredGameActivitiesView { get; }
    public ObservableCollection<BirthdayCandidateCard> BirthdayCandidates { get; } = [];
    public ObservableCollection<BgiCommitCard> BgiCommits { get; } = [];
    public ObservableCollection<BgiCommitCard> BgiMainCommits { get; } = [];
    public ObservableCollection<BgiCommitCard> BgiScriptsCommits { get; } = [];
    public ObservableCollection<ReportSectionCard> ReportSections { get; } = [];
    public ObservableCollection<ReportSectionCard> DeletedReportSections { get; } = [];
    public ObservableCollection<ProviderHealthCard> ProviderHealth { get; } = [];
    public ObservableCollection<SchedulerTaskCard> SchedulerTasks { get; } = [];
    public ObservableCollection<ManualEventCard> ManualEvents { get; } = [];
    public ObservableCollection<ManualBannerCard> ManualBanners { get; } = [];
    public ObservableCollection<ManualVersionCard> ManualVersions { get; } = [];
    public ObservableCollection<EndgameRuleCard> EndgameRules { get; } = [];
    public ObservableCollection<EndgameOccurrenceCard> EndgameOccurrences { get; } = [];
    public ObservableCollection<BannerCharacterEditor> BannerCharacterEditors { get; } = [];
    public ObservableCollection<ImportPreviewCard> ImportPreviewEntries { get; } = [];
    public IReadOnlyList<string> GameFilterOptions { get; } = ["ALL", "GENSHIN", "STARRAIL", "NTE"];
    public IReadOnlyList<string> ManualGameOptions { get; } = ["GENSHIN", "STARRAIL", "NTE", "HI3"];
    public IReadOnlyList<string> ManualBannerTypeOptions { get; } = ["\u4e0a\u534a\u5361\u6c60", "\u4e0b\u534a\u5361\u6c60", "\u7279\u6b8a\u5361\u6c60", "\u89d2\u8272\u6c60"];
    public IReadOnlyList<string> TypeFilterOptions { get; } = ["ALL", "EVENT", "GACHA", "ENDGAME", "VIDEO", "PREVIEW_NOTICE", "PREVIEW_LIVE"];
    public IReadOnlyList<string> CalendarKindOptions { get; } = ["全部", "BIRTHDAY", "ANNIVERSARY", "FESTIVAL", "SOLAR_TERM", "MEMORIAL", "GAME"];
    public IReadOnlyList<string> CalendarFranchiseOptions { get; } = ["全部", "GENSHIN", "STARRAIL", "NTE", "HI3"];
    public IReadOnlyList<string> BirthdayFranchiseOptions { get; } = [string.Empty, "GENSHIN", "HI3", "NTE"];
    public IReadOnlyList<string> ActivityTypeOptions { get; } = ["EVENT", "GACHA", "ENDGAME", "VIDEO", "PREVIEW_NOTICE", "PREVIEW_LIVE"];
    public IReadOnlyList<string> ActivityVerificationOptions { get; } = ["Unverified", "VerifiedOfficial", "VerifiedMultiSource", "Conflict"];
    public IReadOnlyList<string> ActivityTimePrecisionOptions { get; } = ["Exact", "DateOnly", "Relative"];
    public IReadOnlyList<string> ActivityGachaPoolKindOptions { get; } = ["", "CHARACTER", "SPECIAL", "LIGHT_CONE", "CHRONICLED", "UNKNOWN"];
    public IReadOnlyList<string> ActivityGachaPoolPhaseOptions { get; } = ["", "FIRST_HALF", "SECOND_HALF", "FULL_VERSION", "UNKNOWN"];

    public ShellViewModel(QimiaoDailyDbContext database, int pendingReviewCount, int confirmedCount, int pendingArtworkCount, int selectedCommitCount, int enabledCalendarCount, string importMessage)
    {
        StoredGameActivitiesView = CollectionViewSource.GetDefaultView(StoredGameActivities);
        StoredGameActivitiesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GameActivityCard.DisplayGame)));
        StoredGameActivitiesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GameActivityCard.StoredCategory)));
        PendingReviewCount = pendingReviewCount;
        ConfirmedCount = confirmedCount;
        PendingArtworkCount = pendingArtworkCount;
        SelectedCommitCount = selectedCommitCount;
        EnabledCalendarCount = enabledCalendarCount;
        ImportMessage = importMessage;
        var china = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time");
        DailyReportLabel = "\u7eee\u55b5\u65e5\u62a5 " + china.ToString("yyMMdd");
        _calendarDate = DateOnly.FromDateTime(china.Date);
        _connectionString = database.Database.GetDbConnection().ConnectionString;
        PixivSessionConfigured = new SecureSettingsStore(_paths).Has("pixiv_session");
        LoadActivities();
        LoadArtworks();
        LoadArtworkRun();
        LoadCalendar();
        LoadManualData();
        LoadReportWorkspace();
        _remainingTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _remainingTimer.Tick += (_, _) => RefreshRemainingTimes();
        _remainingTimer.Start();
    }

    [ObservableProperty] private int _pendingReviewCount;
    [ObservableProperty] private int _confirmedCount;
    [ObservableProperty] private int _pendingArtworkCount;
    [ObservableProperty] private int _selectedCommitCount;
    [ObservableProperty] private int _enabledCalendarCount;
    [ObservableProperty] private string _importMessage = string.Empty;
    [ObservableProperty] private string _currentPage = "\u6982\u89c8";
    [ObservableProperty] private string _pageHeadline = "\u4eca\u65e5\u7eee\u55b5\u65e5\u62a5\u5b9e\u65f6\u9884\u89c8";
    [ObservableProperty] private string _dailyReportLabel = string.Empty;
    [ObservableProperty] private bool _isGameActivitiesPage;
    [ObservableProperty] private bool _isOverviewPage = true;
    [ObservableProperty] private bool _isArtworkPage;
    [ObservableProperty] private bool _isCalendarPage;
    [ObservableProperty] private bool _isBgiPage;
    [ObservableProperty] private bool _isReportPage;
    [ObservableProperty] private string _composedReport = string.Empty;
    [ObservableProperty] private bool _isSourceHealthPage;
    [ObservableProperty] private bool _isSchedulerPage;
    [ObservableProperty] private bool _isSettingsPage;
    [ObservableProperty] private bool _isArchivePage;
    [ObservableProperty] private bool _isBirthdayEditorPage;
    [ObservableProperty] private bool _isActivityEditorPage;
    [ObservableProperty] private bool _isImportPanelOpen = true;
    [ObservableProperty] private string _selectedCalendarDetail = string.Empty;
    [ObservableProperty] private string _newCalendarEventTitle = string.Empty;
    [ObservableProperty] private string _newCalendarEventKind = "MEMORIAL";
    [ObservableProperty] private Guid? _editingAnniversaryId;
    [ObservableProperty] private string _anniversaryTitle = string.Empty;
    [ObservableProperty] private string _anniversaryStartedOn = string.Empty;
    [ObservableProperty] private string _anniversaryNotes = string.Empty;
    [ObservableProperty] private string _gameFilter = "ALL";
    [ObservableProperty] private string _typeFilter = "ALL";
    [ObservableProperty] private string _calendarSearchText = string.Empty;
    [ObservableProperty] private string _calendarKindFilter = "全部";
    [ObservableProperty] private string _calendarFranchiseFilter = "全部";
    [ObservableProperty] private bool _pixivSessionConfigured;
    [ObservableProperty] private bool _isEvidenceOpen;
    [ObservableProperty] private EvidenceDrawerCard? _selectedEvidence;
    [ObservableProperty] private string _bgiWindowText = string.Empty;
    [ObservableProperty] private string _bgiWindowStatus = string.Empty;
    [ObservableProperty] private string _bgiWindowStatusDisplay = string.Empty;
    [ObservableProperty] private bool _bgiWindowIncomplete;
    [ObservableProperty] private string _artworkRunCountText = "0/30";
    [ObservableProperty] private string _artworkRunStatus = "NOT_RUN";
    public string ArtworkRunStatusDisplay => DisplayNameMapper.ParserStatus(ArtworkRunStatus);
    [ObservableProperty] private string _artworkRunMessage = "尚未执行美图每日采集。";
    [ObservableProperty] private string _artworkRunTimeText = "-";
    [ObservableProperty] private int _selectedArtworkCount;
    [ObservableProperty] private bool _isArtworkEditorPage;
    [ObservableProperty] private Guid? _editingArtworkId;
    [ObservableProperty] private string _artworkEditorModeText = "编辑美图元数据";
    [ObservableProperty] private string _artworkEditTitle = string.Empty;
    [ObservableProperty] private string _artworkEditCharacterName = string.Empty;
    [ObservableProperty] private string _artworkEditFranchiseName = string.Empty;
    [ObservableProperty] private string _artworkEditCategory = string.Empty;
    [ObservableProperty] private string _artworkEditTags = string.Empty;
    [ObservableProperty] private string _artworkBatchReason = "Desktop artwork review operation";
    [ObservableProperty] private Guid? _editingBirthdayId;
    [ObservableProperty] private string _birthdayEditorModeText = "新建生日候选";
    [ObservableProperty] private string _birthdayCharacter = string.Empty;
    [ObservableProperty] private string _birthdayFranchise = "GENSHIN";
    [ObservableProperty] private string _birthdayMonthText = string.Empty;
    [ObservableProperty] private string _birthdayDayText = string.Empty;
    [ObservableProperty] private string _birthdaySource = string.Empty;
    [ObservableProperty] private string _birthdaySourceUrl = string.Empty;
    [ObservableProperty] private string _birthdayEvidence = string.Empty;
    [ObservableProperty] private Guid? _editingActivityId;
    [ObservableProperty] private string _activityEditorModeText = "编辑游戏候选";
    [ObservableProperty] private string _activityEditType = "EVENT";
    [ObservableProperty] private string _activityEditTitle = string.Empty;
    [ObservableProperty] private string _activityEditSourceTime = string.Empty;
    [ObservableProperty] private string _activityEditSourceTimezone = "Asia/Shanghai";
    [ObservableProperty] private string _activityEditNormalizedText = string.Empty;
    [ObservableProperty] private string _activityEditEndText = string.Empty;
    [ObservableProperty] private string _activityEditFetchedText = string.Empty;
    [ObservableProperty] private string _activityEditVerification = "Unverified";
    [ObservableProperty] private string _activityEditTimePrecision = "DateOnly";
    [ObservableProperty] private string _activityEditGachaPoolKind = string.Empty;
    [ObservableProperty] private string _activityEditGachaPoolPhase = string.Empty;
    [ObservableProperty] private string _activityEditGachaGroupKey = string.Empty;
    [ObservableProperty] private string _activityEditReason = string.Empty;
    [ObservableProperty] private string _manualGame = "GENSHIN";
    [ObservableProperty] private int _gameActivityTabIndex;
    [ObservableProperty] private string _manualEventName = string.Empty;
    [ObservableProperty] private string _manualEventStart = string.Empty;
    [ObservableProperty] private string _manualEventEnd = string.Empty;
    [ObservableProperty] private string _manualEventNotes = string.Empty;
    [ObservableProperty] private string _manualEventEditorModeText = "\u65b0\u589e\u6d3b\u52a8";
    [ObservableProperty] private Guid? _editingManualEventId;
    [ObservableProperty] private string _manualBannerName = string.Empty;
    [ObservableProperty] private string _manualBannerType = "\u4e0a\u534a\u5361\u6c60";
    [ObservableProperty] private string _manualBannerCharacters = string.Empty;
    [ObservableProperty] private string _manualBannerStart = string.Empty;
    [ObservableProperty] private string _manualBannerEnd = string.Empty;
    [ObservableProperty] private string _manualBannerNotes = string.Empty;
    [ObservableProperty] private Guid? _editingManualBannerId;
    [ObservableProperty] private string _manualBannerEditorModeText = "\u65b0\u589e\u5361\u6c60";
    [ObservableProperty] private string _manualVersionNumber = string.Empty;
    [ObservableProperty] private string _manualVersionName = string.Empty;
    [ObservableProperty] private string _manualVersionStart = string.Empty;
    [ObservableProperty] private string _manualVersionEnd = string.Empty;
    [ObservableProperty] private string _manualVersionNotes = string.Empty;
    [ObservableProperty] private Guid? _editingVersionId;
    [ObservableProperty] private Guid? _endgameRuleToAdjustId;
    [ObservableProperty] private Guid? _endgameOccurrenceToAdjustId;
    [ObservableProperty] private string _endgameAnchorDate = string.Empty;
    [ObservableProperty] private string _endgameOverrideScheduledDate = string.Empty;
    [ObservableProperty] private string _endgameOverrideDate = string.Empty;
    [ObservableProperty] private string _endgameOverrideStartTime = string.Empty;
    [ObservableProperty] private string _endgameOverrideEndTime = string.Empty;
    [ObservableProperty] private string _endgameOverrideNotes = string.Empty;
    [ObservableProperty] private string _importJsonText = "{\"schemaVersion\":1,\"events\":[],\"banners\":[],\"versions\":[],\"birthdays\":[],\"anniversaries\":[]}";

    partial void OnGameFilterChanged(string value) => RefreshActivities();
    partial void OnTypeFilterChanged(string value) => RefreshActivities();
    partial void OnArtworkRunStatusChanged(string value) => OnPropertyChanged(nameof(ArtworkRunStatusDisplay));
    partial void OnCalendarSearchTextChanged(string value) => ApplyCalendarFilters();
    partial void OnCalendarKindFilterChanged(string value) => ApplyCalendarFilters();
    partial void OnCalendarFranchiseFilterChanged(string value) => ApplyCalendarFilters();

    [RelayCommand]
    private void Navigate(string page)
    {
        CurrentPage = page;
        if (page == "游戏活动") IsImportPanelOpen = false;
        IsOverviewPage = page == "\u6982\u89c8";
        IsGameActivitiesPage = page == "\u6e38\u620f\u6d3b\u52a8";
        IsArtworkPage = page == "\u7f8e\u56fe\u5206\u4eab";
        IsArtworkEditorPage = page == "美图编辑";
        IsCalendarPage = page == "\u65e5\u5386";
        IsBgiPage = page == "BGI";
        IsReportPage = page == "\u65e5\u62a5\u7f16\u8f91\u5668";
        IsSourceHealthPage = page == "\u6765\u6e90\u5065\u5eb7";
        IsSchedulerPage = page == "\u4efb\u52a1\u8c03\u5ea6";
        IsSettingsPage = page == "\u8bbe\u7f6e";
        IsArchivePage = page == "\u5f52\u6863";
        IsBirthdayEditorPage = page == "生日审核";
        IsActivityEditorPage = page == "游戏编辑";
        if (IsBgiPage) LoadBgiCommits();
        if (IsReportPage) LoadReportWorkspace();
        if (IsOverviewPage) LoadReportWorkspace();
        if (IsSourceHealthPage || IsSchedulerPage) LoadOperations();
        if (IsBirthdayEditorPage) LoadCalendar();
        PageHeadline = page == "\u6982\u89c8" ? "\u4eca\u65e5\u7eee\u55b5\u65e5\u62a5\u5b9e\u65f6\u9884\u89c8" : page;
    }

    [RelayCommand]
    private void OpenImportPanel()
    {
        IsImportPanelOpen = false;
        ImportEditorRequested?.Invoke();
    }

    [RelayCommand]
    private void CloseImportPanel() => IsImportPanelOpen = false;

    public void ShowPage(string page) => Navigate(page);

    public void RefreshArtworkCards() => RefreshArtworks();

    private void RefreshRemainingTimes()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var now = DateTimeOffset.UtcNow;
        ActivityRemainingRefresher.Refresh(PendingActivities, now, zone);
        ActivityRemainingRefresher.Refresh(ConfirmedActivities, now, zone);
        ActivityRemainingRefresher.Refresh(ArchivedActivities, now, zone);
    }

    public void SavePixivSession(string cookie)
    {
        new SecureSettingsStore(_paths).Set("pixiv_session", cookie);
        PixivSessionConfigured = true;
    }

    public void ClearPixivSession()
    {
        new SecureSettingsStore(_paths).Delete("pixiv_session");
        PixivSessionConfigured = false;
    }

    [RelayCommand]
    private async Task ConfirmActivityAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new TimelineReviewService(database).ConfirmAsync(id, "desktop-user", "Desktop review confirmation", DateTimeOffset.UtcNow);
        RefreshActivities();
    }

    [RelayCommand]
    private async Task ReturnActivityAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new TimelineReviewService(database).ReturnAsync(id, "desktop-user", "Desktop review return", DateTimeOffset.UtcNow);
        RefreshActivities();
    }

    [RelayCommand]
    private async Task ArchiveActivityAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new TimelineReviewService(database).ArchiveAsync(id, "desktop-user", "Desktop review rejection", DateTimeOffset.UtcNow);
        RefreshActivities();
    }

    [RelayCommand]
    private async Task RestoreArchivedAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new TimelineReviewService(database).RestoreAsync(id, "desktop-user", "Desktop archive restore", DateTimeOffset.UtcNow);
        RefreshActivities();
        Navigate("\u5f52\u6863");
    }

    [RelayCommand]
    private async Task ConfirmArtworkAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new ArtworkImportService(database).ConfirmAsync(id);
        RefreshArtworks();
    }

    [RelayCommand]
    private async Task ReturnArtworkAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new ArtworkImportService(database).ReturnToReviewAsync(id);
        RefreshArtworks();
    }

    [RelayCommand]
    private async Task ToggleArtworkReportAsync(Guid id)
    {
        await using var database = NewDatabase();
        var item = await database.Artworks.SingleAsync(x => x.Id == id);
        var selecting = !item.SelectedForReport;
        await new ArtworkImportService(database).SetSelectedForReportAsync(id, selecting);
        if (!selecting)
        {
            ImportMessage = "已取消选入今日日报；已下载的本地图片会保留。";
            RefreshArtworks();
            return;
        }

        try
        {
            var cookie = OperatingSystem.IsWindows() ? new SecureSettingsStore(_paths).TryGet("pixiv_session") : null;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var cached = await new ArtworkThumbnailCacheService(client, _paths, cookie).TryCacheAsync(item);
            if (cached)
            {
                await database.SaveChangesAsync();
                ImportMessage = "已选入今日日报，并已下载图片；现在可直接复制。";
            }
            else
            {
                ImportMessage = "已选入今日日报，但图片下载失败；下次启动会自动重试。";
            }
        }
        catch
        {
            ImportMessage = "已选入今日日报，但图片下载失败；下次启动会自动重试。";
        }
        RefreshArtworks();
    }

    [RelayCommand]
    private void ToggleArtworkMarked(Guid id)
    {
        ReplaceArtworkCard(PendingArtworks, id);
        ReplaceArtworkCard(ConfirmedArtworks, id);
        SelectedArtworkCount = PendingArtworks.Count(x => x.IsMarked) + ConfirmedArtworks.Count(x => x.IsMarked);
    }

    [RelayCommand]
    private async Task BatchConfirmArtworkAsync()
    {
        var ids = PendingArtworks.Where(x => x.IsMarked).Select(x => x.Id).ToArray();
        if (ids.Length == 0) { ImportMessage = "没有标记待审核美图。"; return; }
        try
        {
            await using var database = NewDatabase();
            var count = await new ArtworkImportService(database).BatchConfirmAsync(ids, "desktop-user", ArtworkBatchReason, DateTimeOffset.UtcNow);
            ImportMessage = $"已批量确认 {count} 张美图。";
            RefreshArtworks();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ImportMessage = "批量确认失败：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task BatchReturnArtworkAsync()
    {
        var ids = ConfirmedArtworks.Where(x => x.IsMarked).Select(x => x.Id).ToArray();
        if (ids.Length == 0) { ImportMessage = "没有标记已确认美图。"; return; }
        try
        {
            await using var database = NewDatabase();
            var count = await new ArtworkImportService(database).BatchReturnToReviewAsync(ids, "desktop-user", ArtworkBatchReason, DateTimeOffset.UtcNow);
            ImportMessage = $"已批量退回 {count} 张美图到审核区。";
            RefreshArtworks();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ImportMessage = "批量退回失败：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task BatchDeleteArtworkAsync()
    {
        var ids = PendingArtworks.Where(x => x.IsMarked).Select(x => x.Id)
            .Concat(ConfirmedArtworks.Where(x => x.IsMarked).Select(x => x.Id)).ToArray();
        if (ids.Length == 0) { ImportMessage = "没有标记美图。"; return; }
        try
        {
            await using var database = NewDatabase();
            var count = await new ArtworkImportService(database).BatchDeleteAsync(ids, "desktop-user", ArtworkBatchReason, DateTimeOffset.UtcNow);
            ImportMessage = $"已删除 {count} 张美图候选；永久去重记录已保留。";
            RefreshArtworks();
        }
        catch (ArgumentException ex)
        {
            ImportMessage = "批量删除失败：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteArtworkAsync(Guid id)
    {
        try
        {
            await using var database = NewDatabase();
            await new ArtworkImportService(database).DeleteAsync(id, "desktop-user", ArtworkBatchReason, DateTimeOffset.UtcNow);
            ImportMessage = "美图候选已删除；永久去重记录已保留。";
            RefreshArtworks();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ImportMessage = "美图删除失败：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    [RelayCommand]
    private void EditArtwork(Guid id)
    {
        using var database = NewDatabase();
        var item = database.Artworks.AsNoTracking().SingleOrDefault(x => x.Id == id);
        if (item is null) return;
        EditingArtworkId = item.Id;
        ArtworkEditorModeText = $"编辑美图：{item.Title}";
        ArtworkEditTitle = item.Title;
        ArtworkEditCharacterName = item.CharacterName;
        ArtworkEditFranchiseName = item.FranchiseName;
        ArtworkEditCategory = item.Category;
        ArtworkEditTags = item.Tags;
        ArtworkBatchReason = string.Empty;
        IsEvidenceOpen = false;
        SelectedEvidence = null;
        ArtworkEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveArtworkEditAsync()
    {
        if (EditingArtworkId is not { } id)
        {
            ImportMessage = "美图未保存：没有选择候选记录。";
            return;
        }
        try
        {
            await using var database = NewDatabase();
            var changed = await new ArtworkImportService(database).EditMetadataAsync(id, ArtworkEditTitle, ArtworkEditCharacterName, ArtworkEditFranchiseName, ArtworkEditCategory, ArtworkEditTags, "desktop-user", ArtworkBatchReason, DateTimeOffset.UtcNow);
            ImportMessage = changed == 0 ? "美图没有字段变化。" : $"美图已保存 {changed} 个字段变化，并保留 Revision History。";
            RefreshArtworks();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ImportMessage = "美图未保存：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    public async Task HandleArtworkShortcutAsync(string shortcut)
    {
        if (!IsArtworkPage) return;
        switch (shortcut.Trim().ToUpperInvariant())
        {
            case "C": await BatchConfirmArtworkAsync(); break;
            case "R": await BatchReturnArtworkAsync(); break;
            case "D": await BatchDeleteArtworkAsync(); break;
        }
    }

    private static void ReplaceArtworkCard(ObservableCollection<ArtworkCard> cards, Guid id)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (cards[index].Id != id) continue;
            cards[index] = cards[index] with { IsMarked = !cards[index].IsMarked };
            return;
        }
    }

    [RelayCommand]
    private void OpenArtworkSource(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    [RelayCommand]
    private void CopyArtworkImage(Guid id)
    {
        var artwork = FindArtwork(id);
        if (artwork is null) return;
        if (File.Exists(artwork.ThumbnailUrl))
        {
            try
            {
                using var input = File.OpenRead(artwork.ThumbnailUrl);
                var image = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                image.Freeze();
                Clipboard.SetImage(image);
                ImportMessage = "已复制图片到剪贴板，可直接粘贴。";
            }
            catch
            {
                var files = new StringCollection { artwork.ThumbnailUrl };
                Clipboard.SetFileDropList(files);
                ImportMessage = "图片无法作为位图复制，已复制图片文件到剪贴板。";
            }
            return;
        }

        Clipboard.SetText(artwork.SourceUrl);
        ImportMessage = "本地图片尚未缓存，已复制原图页面链接。";
    }

    [RelayCommand]
    private void OpenArtworkFolder(Guid id)
    {
        var artwork = FindArtwork(id);
        if (artwork is null) return;
        if (File.Exists(artwork.ThumbnailUrl))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{artwork.ThumbnailUrl}\"") { UseShellExecute = true });
            return;
        }

        _paths.EnsureDirectories();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_paths.ImagesDirectory) { UseShellExecute = true });
        ImportMessage = "图片尚未缓存，已打开图片缓存目录。";
    }

    [RelayCommand]
    private void OpenReportOutputDirectory()
    {
        _paths.EnsureDirectories();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_paths.ReportsDirectory) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenImageCacheDirectory()
    {
        _paths.EnsureDirectories();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_paths.ImagesDirectory) { UseShellExecute = true });
    }

    private ArtworkCard? FindArtwork(Guid id) => PendingArtworks.Concat(ConfirmedArtworks).FirstOrDefault(card => card.Id == id);

    [RelayCommand]
    private void SelectCalendarDate(DateTime date)
    {
        _calendarDate = DateOnly.FromDateTime(date);
        var items = MonthCalendarDays.Where(x => x.Date.Date == date.Date).ToList();
        SelectedCalendarDetail = items.Count == 0 ? date.ToString("yyyy-MM-dd") : date.ToString("yyyy-MM-dd") + "  " + string.Join(" · ", items.Select(x => x.Details));
    }

    public void ShowCalendarDate(DateTime date) => SelectCalendarDate(date);

    [RelayCommand]
    private async Task OpenEvidenceAsync(Guid id)
    {
        await using var database = NewDatabase();
        var item = await database.TimelineItems.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return;
        var evidence = item.Evidence.OrderByDescending(x => x.FetchedAt).FirstOrDefault();
        if (evidence is null) return;
        var providers = string.Join(Environment.NewLine, item.Evidence.Select(x => DisplayNameMapper.Provider(x.SourceProvider)).Distinct(StringComparer.OrdinalIgnoreCase));
        var sourceUrls = string.Join(Environment.NewLine, item.Evidence.Select(x => x.SourceUrl).Distinct(StringComparer.OrdinalIgnoreCase));
        var sourceText = string.Join(Environment.NewLine + Environment.NewLine,
            item.Evidence.Select(x => $"[{DisplayNameMapper.Provider(x.SourceProvider)}] {x.SourceText}"));
        var history = (await database.TimelineItemRevisions.AsNoTracking()
                .Where(x => x.TimelineItemId == id)
            .ToListAsync())
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => $"{x.CreatedAt:yyyy-MM-dd HH:mm} {DisplayNameMapper.RevisionField(x.FieldName)}: {DisplayNameMapper.RevisionValue(x.FieldName, x.OldValue)} → {DisplayNameMapper.RevisionValue(x.FieldName, x.NewValue)}（{DisplayNameMapper.RevisionReason(x.Reason)}）")
            .ToList();
        SelectedEvidence = new EvidenceDrawerCard(
            item.Id,
            item.GameCode, item.ItemType, item.Title, providers, sourceUrls, sourceText,
            item.SourceTime ?? "-", evidence.NormalizedTime?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? item.NormalizedTime?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-",
            evidence.PublishedAt?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-", evidence.FetchedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            evidence.OriginalTimezone ?? item.SourceTimezone ?? "-", evidence.ParserVersion, item.VerificationStatus.ToString(),
            history.Count == 0 ? "无历史变更" : string.Join(Environment.NewLine, history));
        IsEvidenceOpen = true;
    }

    [RelayCommand]
    private void EditActivity(Guid id)
    {
        using var database = NewDatabase();
        var item = database.TimelineItems.AsNoTracking().SingleOrDefault(x => x.Id == id);
        if (item is null) return;

        EditingActivityId = item.Id;
        ActivityEditorModeText = $"编辑游戏候选：{item.Title}";
        ActivityEditType = item.ItemType;
        ActivityEditTitle = item.Title;
        ActivityEditSourceTime = item.SourceTime ?? string.Empty;
        ActivityEditSourceTimezone = item.SourceTimezone ?? "Asia/Shanghai";
        ActivityEditNormalizedText = item.NormalizedTime?.ToString("O") ?? string.Empty;
        ActivityEditEndText = item.EndAt?.ToString("O") ?? string.Empty;
        ActivityEditFetchedText = item.FetchedAt.ToString("O");
        ActivityEditVerification = item.VerificationStatus.ToString();
        ActivityEditTimePrecision = item.TimePrecision.ToString();
        ActivityEditGachaPoolKind = item.GachaPoolKind ?? string.Empty;
        ActivityEditGachaPoolPhase = item.GachaPoolPhase ?? string.Empty;
        ActivityEditGachaGroupKey = item.GachaGroupKey ?? string.Empty;
        ActivityEditReason = string.Empty;
        IsEvidenceOpen = false;
        SelectedEvidence = null;
        ActivityEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveActivityEditAsync()
    {
        if (EditingActivityId is not { } itemId)
        {
            ImportMessage = "游戏候选未保存：没有选择候选记录。";
            return;
        }

        if (!Enum.TryParse(ActivityEditVerification, true, out VerificationStatus verification) || !Enum.IsDefined(verification))
        {
            ImportMessage = "游戏候选未保存：验证状态无效。";
            return;
        }
        if (!Enum.TryParse(ActivityEditTimePrecision, true, out TimePrecision precision) || !Enum.IsDefined(precision))
        {
            ImportMessage = "游戏候选未保存：时间精度无效。";
            return;
        }

        try
        {
            var normalized = ParseOptionalActivityTime(ActivityEditNormalizedText, "标准时间", ActivityEditSourceTimezone);
            var endAt = ParseOptionalActivityTime(ActivityEditEndText, "结束时间", ActivityEditSourceTimezone);
            var fetchedAt = ParseRequiredActivityTime(ActivityEditFetchedText, "抓取时间", ActivityEditSourceTimezone);
            await using var database = NewDatabase();
            await new TimelineReviewService(database).EditAsync(
                itemId,
                ActivityEditType,
                ActivityEditTitle,
                verification,
                string.IsNullOrWhiteSpace(ActivityEditSourceTime) ? null : ActivityEditSourceTime,
                string.IsNullOrWhiteSpace(ActivityEditSourceTimezone) ? null : ActivityEditSourceTimezone,
                normalized,
                precision,
                fetchedAt,
                endAt,
                "desktop-user",
                ActivityEditReason,
                DateTimeOffset.UtcNow,
                gachaPoolKind: string.IsNullOrWhiteSpace(ActivityEditGachaPoolKind) ? null : ActivityEditGachaPoolKind,
                gachaPoolPhase: string.IsNullOrWhiteSpace(ActivityEditGachaPoolPhase) ? null : ActivityEditGachaPoolPhase,
                gachaGroupKey: string.IsNullOrWhiteSpace(ActivityEditGachaGroupKey) ? null : ActivityEditGachaGroupKey);

            ImportMessage = "游戏候选已保存，并已退回待审核区。";
            RefreshActivities();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            ImportMessage = "游戏候选未保存：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    [RelayCommand]
    private void CloseEvidence()
    {
        IsEvidenceOpen = false;
        SelectedEvidence = null;
    }

    [RelayCommand]
    private void OpenEvidenceSource(string url)
    {
        var first = url.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (Uri.TryCreate(first, UriKind.Absolute, out var uri))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task AddCalendarEventAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCalendarEventTitle)) return;
        await using var database = NewDatabase();
        await new CalendarService(database).AddCustomEventAsync(_calendarDate, NewCalendarEventTitle, NewCalendarEventKind);
        NewCalendarEventTitle = string.Empty;
        LoadCalendar();
    }

    [RelayCommand]
    private async Task DeleteCalendarEventAsync(Guid id)
    {
        await using var database = NewDatabase();
        await new CalendarService(database).DeleteCustomEventAsync(id);
        LoadCalendar();
    }

    [RelayCommand]
    private void BeginAnniversaryCreate()
    {
        ResetAnniversaryEditor();
        AnniversaryEditorRequested?.Invoke();
    }

    [RelayCommand]
    private void EditAnniversary(Guid id)
    {
        using var database = NewDatabase();
        var item = database.Anniversaries.AsNoTracking().SingleOrDefault(x => x.Id == id);
        if (item is null) return;
        EditingAnniversaryId = item.Id;
        AnniversaryTitle = item.Title;
        AnniversaryStartedOn = item.StartedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        AnniversaryNotes = item.Notes;
        ImportMessage = $"正在编辑纪念日：{item.Title}。";
        AnniversaryEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAnniversaryAsync()
    {
        if (!DateOnly.TryParseExact(AnniversaryStartedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            ImportMessage = "纪念日未保存：日期必须使用 yyyy-MM-dd。";
            return;
        }
        try
        {
            await using var database = NewDatabase();
            await new AnniversaryService(database).SaveAsync(EditingAnniversaryId, new AnniversaryInput(AnniversaryTitle, date, AnniversaryNotes));
            ResetAnniversaryEditor();
            LoadCalendar();
            ImportMessage = "纪念日已保存为人工确认数据。";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ImportMessage = "纪念日未保存：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleAnniversaryEnabledAsync(Guid id)
    {
        try
        {
            await using var database = NewDatabase();
            var item = await database.Anniversaries.SingleAsync(x => x.Id == id);
            await new AnniversaryService(database).SetEnabledAsync(id, !item.Enabled);
            LoadCalendar();
        }
        catch (InvalidOperationException ex)
        {
            ImportMessage = "纪念日状态更新失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task UpdateCalendarEventAsync(CalendarEventCard card)
    {
        await using var database = NewDatabase();
        await new CalendarService(database).UpdateCustomEventAsync(card.Id, DateOnly.FromDateTime(card.Date), card.Title, card.Kind, card.Detail, true);
        LoadCalendar();
    }

    [RelayCommand]
    private async Task ToggleBirthdayEnabledAsync(Guid id)
    {
        await using var database = NewDatabase();
        var item = await database.Birthdays.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return;
        try
        {
            await new BirthdayReviewService(database).SetEnabledAsync(id, !item.Enabled);
            ImportMessage = item.Enabled ? $"已停用生日：{item.Character}" : $"已启用生日：{item.Character}";
        }
        catch (InvalidOperationException ex)
        {
            ImportMessage = "生日未启用：" + DisplayNameMapper.ProviderError(ex.Message);
        }
        LoadCalendar();
    }

    [RelayCommand]
    private void BeginBirthdayCreate()
    {
        ResetBirthdayEditor();
        BirthdayEditorRequested?.Invoke();
    }

    private void ResetBirthdayEditor()
    {
        EditingBirthdayId = null;
        BirthdayEditorModeText = "新建生日候选";
        BirthdayCharacter = string.Empty;
        BirthdayFranchise = string.Empty;
        BirthdayMonthText = string.Empty;
        BirthdayDayText = string.Empty;
        BirthdaySource = string.Empty;
        BirthdaySourceUrl = string.Empty;
        BirthdayEvidence = string.Empty;
    }

    [RelayCommand]
    private void EditBirthday(Guid id)
    {
        using var database = NewDatabase();
        var item = database.Birthdays.AsNoTracking().SingleOrDefault(x => x.Id == id);
        if (item is null) return;
        EditingBirthdayId = id;
        BirthdayEditorModeText = $"编辑生日候选：{item.Character}";
        BirthdayCharacter = DisplayNameMapper.BirthdayCharacter(item.Character, item.Aliases);
        BirthdayFranchise = item.Franchise;
        BirthdayMonthText = item.Month > 0 ? item.Month.ToString() : string.Empty;
        BirthdayDayText = item.Day > 0 ? item.Day.ToString() : string.Empty;
        BirthdaySource = item.Source;
        BirthdaySourceUrl = item.SourceUrl;
        BirthdayEvidence = item.Evidence;
        BirthdayEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveBirthdayAsync()
    {
        if (!int.TryParse(BirthdayMonthText, out var month) || !int.TryParse(BirthdayDayText, out var day))
        {
            ImportMessage = "生日未保存：月和日必须是数字。";
            return;
        }
        try
        {
            await using var database = NewDatabase();
            var id = await new BirthdayReviewService(database).SaveManualAsync(EditingBirthdayId, BirthdayCharacter, BirthdayFranchise, month, day, "MANUAL", string.Empty, string.Empty, VerificationStatus.Unverified);
            ImportMessage = $"生日已保存并启用（{id}）。";
            ResetBirthdayEditor();
            LoadCalendar();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ImportMessage = "生日未保存：" + DisplayNameMapper.ProviderError(ex.Message);
        }
    }

    private void ResetAnniversaryEditor()
    {
        EditingAnniversaryId = null;
        AnniversaryTitle = string.Empty;
        AnniversaryStartedOn = _calendarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        AnniversaryNotes = string.Empty;
        ImportMessage = "请输入纪念日名称和起始日期。";
    }

    [RelayCommand]
    private void OpenCommitSource(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    [RelayCommand]
    private void EditReportSection(ReportSectionCard? card)
    {
        if (card is not null) ReportSectionEditorRequested?.Invoke(card);
    }

    [RelayCommand]
    private async Task SaveReportSectionAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        await new DailyReportService(database).UpdateManualSectionAsync(CurrentReportDate(), card.Key, card.Text);
        card.Dirty = true;
        card.ManualOverride = true;
        ImportMessage = "日报段落已保存：" + card.DisplayName;
    }

    [RelayCommand]
    private async Task RestoreReportSectionAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        var service = new DailyReportService(database);
        await service.RestoreAutomaticSectionAsync(CurrentReportDate(), card.Key);
        await service.BuildAutomaticSectionAsync(CurrentReportDate(), card.Key);
        ImportMessage = "已恢复自动内容：" + card.DisplayName;
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task RebuildReportSectionAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        var service = new DailyReportService(database);
        var rebuilt = await service.BuildAutomaticSectionAsync(CurrentReportDate(), card.Key);
        ImportMessage = rebuilt
            ? "已重新生成：" + card.DisplayName
            : card.IsDeleted
                ? "该日报段落已删除，请先点击“恢复自动”。"
                : "该日报段落包含手工修改，请先点击“恢复自动”后再生成。";
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task DeleteReportSectionAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        await new DailyReportService(database).DeleteSectionAsync(CurrentReportDate(), card.Key);
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task MoveReportSectionUpAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        await new DailyReportService(database).MoveSectionAsync(CurrentReportDate(), card.Key, -1);
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task MoveReportSectionDownAsync(ReportSectionCard card)
    {
        await using var database = NewDatabase();
        await new DailyReportService(database).MoveSectionAsync(CurrentReportDate(), card.Key, 1);
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task CopyReportAsync()
    {
        await using var database = NewDatabase();
        Clipboard.SetText(await new DailyReportService(database).ComposeAsync(CurrentReportDate()));
    }

    [RelayCommand]
    private async Task RebuildReportAsync()
    {
        await using var database = NewDatabase();
        await new DailyReportService(database).BuildAutomaticSectionsAsync(CurrentReportDate());
        LoadReportWorkspace();
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        await using var database = NewDatabase();
        _paths.EnsureDirectories();
        var outputPath = Path.Combine(_paths.ReportsDirectory, $"QimiaoDaily-{CurrentReportDate():yyyy-MM-dd}.md");
        await new DailyReportService(database).ExportAsync(CurrentReportDate(), outputPath, true);
        ImportMessage = "Markdown 日报已导出：" + outputPath;
    }

    [RelayCommand]
    private async Task ExportTextReportAsync()
    {
        await using var database = NewDatabase();
        _paths.EnsureDirectories();
        var outputPath = Path.Combine(_paths.ReportsDirectory, $"QimiaoDaily-{CurrentReportDate():yyyy-MM-dd}.txt");
        await new DailyReportService(database).ExportAsync(CurrentReportDate(), outputPath, false);
        ImportMessage = "TXT 日报已导出：" + outputPath;
    }

    [RelayCommand]
    private async Task RunSchedulerTaskAsync(string taskKey)
    {
        await using var database = NewDatabase();
        try
        {
            await new OperationsService(database).RunNowAsync(taskKey, ct => ExecuteSchedulerTaskAsync(database, taskKey, ct));
        }
        catch
        {
            // OperationsService persists the failure; the page refresh exposes it.
        }
        LoadOperations();
    }

    [RelayCommand]
    private async Task RefreshArtworkDataAsync()
    {
        await using var database = NewDatabase();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        try
        {
            var executor = new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths());
            await new OperationsService(database).RunNowAsync("artwork_daily_search", ct => executor.ExecuteAsync("artwork_daily_search", ct));
            ImportMessage = "美图采集已完成；请在审核区确认新候选。";
        }
        catch
        {
            ImportMessage = "美图采集失败；请在来源健康中查看原因。";
        }
        RefreshArtworks();
        LoadArtworkRun();
    }

    [RelayCommand]
    private async Task RefreshBgiDataAsync()
    {
        await using var database = NewDatabase();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        try
        {
            var executor = new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths());
            await new OperationsService(database).RunNowAsync("github_bgi_refresh", ct => executor.ExecuteAsync("github_bgi_refresh", ct));
            var rebuilt = await new DailyReportService(database).BuildAutomaticSectionAsync(CurrentReportDate(), "bgi");
            ImportMessage = rebuilt
                ? "BGI 两个仓库刷新完成，已同步写入今日日报。"
                : "BGI 两个仓库刷新完成；日报的 BGI 段落已被手工修改，未自动覆盖。";
        }
        catch
        {
            ImportMessage = "BGI 刷新失败；请在来源健康中查看原因。";
        }
        LoadBgiCommits();
        LoadOperations();
    }

    [RelayCommand]
    private void RefreshSourceHealth()
    {
        LoadOperations();
        ImportMessage = "已重新读取最近一次来源检查结果；联网刷新请在对应业务页执行。";
    }

    [RelayCommand]
    private async Task RefreshGameDataAsync()
    {
        await using var database = NewDatabase();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var executor = new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths());
        try
        {
            await new OperationsService(database).RunNowAsync("game_data_refresh", ct => executor.ExecuteAsync("game_data_refresh", ct));
        }
        catch
        {
            // The task and provider health records retain the failure for the UI.
        }
        RefreshActivities();
        ImportMessage = executor.LastGameRefreshReport is { } report
            ? GameRefreshSummaryFormatter.Format(report)
            : "游戏数据刷新失败；请在来源健康中查看错误。";
    }

    [RelayCommand]
    private async Task RefreshBirthdayDataAsync()
    {
        await using var database = NewDatabase();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var executor = new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths());
        try
        {
            await new OperationsService(database).RunNowAsync("birthday_character_refresh", ct => executor.ExecuteAsync("birthday_character_refresh", ct));
            ImportMessage = executor.LastBirthdayRefreshReport is { } report
                ? BirthdayRefreshSummaryFormatter.Format(report)
                : "生日数据刷新完成；未知日期会保持待核验。";
        }
        catch
        {
            ImportMessage = "生日数据刷新失败；请在来源健康中查看错误。";
        }
        LoadCalendar();
    }

    [RelayCommand]
    private void EditStoredGameActivity(GameActivityCard? activity)
    {
        if (activity is null) return;

        var category = activity.Type.Split(" \u8def ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? activity.Type;
        switch (category.ToUpperInvariant())
        {
            case "EVENT":
                GameActivityTabIndex = 0;
                EditManualEvent(activity.Id);
                break;
            case "GACHA":
                GameActivityTabIndex = 1;
                EditManualBanner(activity.Id);
                break;
            case "ENDGAME":
                GameActivityTabIndex = 2;
                SelectEndgameOccurrence(activity.Id);
                break;
            default:
                ImportMessage = "\u8be5\u6761\u76ee\u4e0d\u652f\u6301\u5728\u6b64\u5904\u7f16\u8f91\u3002";
                break;
        }
    }

    [RelayCommand]
    private async Task DeleteStoredGameActivityAsync(GameActivityCard? activity)
    {
        if (activity is null) return;

        try
        {
            var category = activity.Type.Split(" \u8def ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? activity.Type;
            await using var database = NewDatabase();
            switch (category.ToUpperInvariant())
            {
                case "EVENT":
                    await new ManualDataService(database).ArchiveEventAsync(activity.Id);
                    ImportMessage = "\u6d3b\u52a8\u5df2\u5220\u9664\uff0c\u5e76\u79fb\u5165\u5f52\u6863\u3002";
                    break;
                case "GACHA":
                    await new ManualDataService(database).ArchiveBannerAsync(activity.Id);
                    ImportMessage = "\u5361\u6c60\u5df2\u5220\u9664\uff0c\u5e76\u79fb\u5165\u5f52\u6863\u3002";
                    break;
                case "ENDGAME":
                {
                    var occurrence = await database.EndgameOccurrences.SingleOrDefaultAsync(x => x.Id == activity.Id);
                    if (occurrence is null)
                    {
                        ImportMessage = "\u672a\u627e\u5230\u8981\u5220\u9664\u7684\u6df1\u6e0a\u671f\u6b21\u3002";
                        return;
                    }

                    var scheduledDate = occurrence.ScheduledDate
                        ?? occurrence.OccurrenceDate
                        ?? DateOnly.FromDateTime(occurrence.StartAt.DateTime);
                    await new EndgameScheduleMaintenanceService(database).OverrideAsync(
                        occurrence.RuleId,
                        new EndgameOccurrenceOverride(scheduledDate, Suppressed: true, Notes: "\u7528\u6237\u5220\u9664\u8be5\u671f"),
                        _calendarDate);
                    ImportMessage = "\u8be5\u671f\u6df1\u6e0a\u5df2\u5220\u9664\uff1b\u5468\u671f\u89c4\u5219\u4ecd\u4f1a\u7ee7\u7eed\u8ba1\u7b97\u540e\u7eed\u671f\u6b21\u3002";
                    break;
                }
                default:
                    ImportMessage = "\u8be5\u6761\u76ee\u4e0d\u652f\u6301\u5728\u6b64\u5904\u5220\u9664\u3002";
                    return;
            }

            LoadManualData();
            RefreshActivities();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ImportMessage = "\u5220\u9664\u5931\u8d25\uff1a" + ex.Message;
        }
    }

    [RelayCommand]
    private void BeginManualEventCreate()
    {
        ResetManualEventEditor();
        ImportMessage = "填写活动名称和起止时间后保存；人工记录会直接进入正式数据。";
        ManualEventEditorRequested?.Invoke();
    }

    private void ResetManualEventEditor()
    {
        EditingManualEventId = null;
        ManualEventEditorModeText = "\u65b0\u589e\u6d3b\u52a8";
        ManualEventName = string.Empty;
        ManualEventStart = string.Empty;
        ManualEventEnd = string.Empty;
        ManualEventNotes = string.Empty;
    }

    [RelayCommand]
    private void EditManualEvent(Guid eventId)
    {
        using var database = NewDatabase();
        var item = database.ManualEvents.AsNoTracking().SingleOrDefault(x => x.Id == eventId);
        if (item is null) { ImportMessage = "未找到要编辑的活动。"; return; }
        EditingManualEventId = item.Id;
        ManualEventEditorModeText = $"\u7f16\u8f91\u6d3b\u52a8\uff1a{item.Name}";
        ManualGame = item.Game;
        ManualEventName = item.Name;
        ManualEventStart = item.StartAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualEventEnd = item.EndAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualEventNotes = item.Notes;
        ImportMessage = $"正在编辑活动 {item.Name}；保存后剩余时间会立即重新计算。";
        ManualEventEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task AddManualEventAsync()
    {
        try
        {
            var wasEditing = EditingManualEventId is not null;
            await using var database = NewDatabase();
            var service = new ManualDataService(database);
            var input = new ManualEventInput(
                ManualGame, ManualEventName, ParseRequiredActivityTime(ManualEventStart, "活动开始时间", "Asia/Shanghai", new TimeOnly(4, 0)),
                ParseRequiredActivityTime(ManualEventEnd, "活动结束旴间", "Asia/Shanghai", new TimeOnly(3, 59)), ManualEventNotes);
            if (EditingManualEventId is Guid eventId) await service.UpdateEventAsync(eventId, input);
            else await service.CreateEventAsync(input);
            ImportMessage = wasEditing ? "活动已更新，剩余时间已重新计算。" : "活动已保存为人工确认的正式数据。";
            ResetManualEventEditor();
            LoadManualData();
            RefreshActivities();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ImportMessage = "活动未保存：" + ex.Message;
        }
    }

    [RelayCommand]
    private void BeginBannerCreate()
    {
        ResetBannerEditor();
        ImportMessage = "角色按输入顺序保存；保存后会直接进入正式数据。";
        BannerEditorRequested?.Invoke();
    }

    private void ResetBannerEditor()
    {
        EditingManualBannerId = null;
        ManualBannerEditorModeText = "\u65b0\u589e\u5361\u6c60";
        ManualBannerName = string.Empty;
        ManualBannerType = "\u4e0a\u534a\u5361\u6c60";
        ManualBannerCharacters = string.Empty;
        BannerCharacterEditors.Clear();
        BannerCharacterEditors.Add(new BannerCharacterEditor());
        ManualBannerStart = string.Empty;
        ManualBannerEnd = string.Empty;
        ManualBannerNotes = string.Empty;
    }

    [RelayCommand]
    private void EditManualBanner(Guid bannerId)
    {
        using var database = NewDatabase();
        var item = database.Banners.Include(x => x.Characters).AsNoTracking().SingleOrDefault(x => x.Id == bannerId);
        if (item is null) { ImportMessage = "未找到要编辑的卡池。"; return; }
        EditingManualBannerId = item.Id;
        ManualBannerEditorModeText = $"\u7f16\u8f91\u5361\u6c60\uff1a{item.Name}";
        ManualGame = item.Game;
        ManualBannerName = item.Name;
        ManualBannerType = string.IsNullOrWhiteSpace(item.CustomType) ? item.Type : item.CustomType!;
        ManualBannerStart = item.StartAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualBannerEnd = item.EndAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualBannerNotes = item.Notes;
        BannerCharacterEditors.Clear();
        foreach (var character in item.Characters.OrderBy(x => x.SortOrder)) BannerCharacterEditors.Add(new BannerCharacterEditor { Name = character.Name });
        if (BannerCharacterEditors.Count == 0) BannerCharacterEditors.Add(new BannerCharacterEditor());
        ImportMessage = $"正在编辑卡池 {item.Name}；角色顺序保持结构化保存。";
        BannerEditorRequested?.Invoke();
    }

    [RelayCommand]
    private void AddBannerCharacter() => BannerCharacterEditors.Add(new BannerCharacterEditor());

    [RelayCommand]
    private void RemoveBannerCharacter(BannerCharacterEditor? editor)
    {
        if (editor is not null) BannerCharacterEditors.Remove(editor);
        if (BannerCharacterEditors.Count == 0) BannerCharacterEditors.Add(new BannerCharacterEditor());
    }

    [RelayCommand]
    private async Task AddManualBannerAsync()
    {
        try
        {
            var wasEditing = EditingManualBannerId is not null;
            var characters = BannerCharacterEditors.Select(x => x.Name.Trim()).Where(x => x.Length > 0).ToArray();
            await using var database = NewDatabase();
            var service = new ManualDataService(database);
            var input = new BannerInput(
                ManualGame, ManualBannerName, ManualBannerType, null,
                ParseRequiredActivityTime(ManualBannerStart, "卡池开始时间", "Asia/Shanghai", new TimeOnly(4, 0)),
                ParseRequiredActivityTime(ManualBannerEnd, "卡池结束旴间", "Asia/Shanghai", new TimeOnly(3, 59)), ManualBannerNotes, characters);
            if (EditingManualBannerId is Guid bannerId) await service.UpdateBannerAsync(bannerId, input);
            else await service.CreateBannerAsync(input);
            ImportMessage = wasEditing ? "卡池已更新，结构化角色顺序已保持。" : "卡池已保存为人工确认的正式数据。";
            ResetBannerEditor();
            LoadManualData();
            RefreshActivities();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ImportMessage = "卡池未保存：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteManualBannerAsync(Guid bannerId)
    {
        await using var database = NewDatabase();
        await new ManualDataService(database).ArchiveBannerAsync(bannerId);
        ImportMessage = "\u5361\u6c60\u5df2\u5220\u9664\uff0c\u5e76\u79fb\u5165\u5f52\u6863\u3002";
        ResetBannerEditor();
        LoadManualData();
        RefreshActivities();
    }

    [RelayCommand]
    private void BeginVersionCreate()
    {
        ResetVersionEditor();
        ImportMessage = "保存时会检查同一游戏版本的时间重叠。";
        VersionEditorRequested?.Invoke();
    }

    private void ResetVersionEditor()
    {
        EditingVersionId = null;
        ManualVersionNumber = string.Empty;
        ManualVersionName = string.Empty;
        ManualVersionStart = string.Empty;
        ManualVersionEnd = string.Empty;
        ManualVersionNotes = string.Empty;
    }

    [RelayCommand]
    private void EditManualVersion(Guid versionId)
    {
        using var database = NewDatabase();
        var version = database.GameVersions.AsNoTracking().SingleOrDefault(x => x.Id == versionId);
        if (version is null)
        {
            ImportMessage = "未找到要编辑的版本。";
            return;
        }

        EditingVersionId = version.Id;
        ManualGame = version.Game;
        ManualVersionNumber = version.VersionNumber;
        ManualVersionName = version.VersionName;
        ManualVersionStart = version.StartAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualVersionEnd = version.EndAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ManualVersionNotes = version.Notes;
        ImportMessage = $"正在编辑版本 {version.VersionNumber}。保存后会重算该版本所属游戏的版本玩法。";
        VersionEditorRequested?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteManualVersionAsync(Guid versionId)
    {
        await using var database = NewDatabase();
        await new ManualDataService(database).ArchiveVersionAsync(versionId);
        ImportMessage = "\u7248\u672c\u5df2\u5220\u9664\uff0c\u5e76\u79fb\u5165\u5f52\u6863\uff1b\u7248\u672c\u76f8\u5173\u5468\u671f\u73a9\u6cd5\u5df2\u91cd\u65b0\u8ba1\u7b97\u3002";
        await new EndgameScheduleMaintenanceService(database).RefreshVersionDependentRulesAsync(_calendarDate);
        ResetVersionEditor();
        LoadManualData();
        RefreshActivities();
    }

    [RelayCommand]
    private async Task AddManualVersionAsync()
    {
        try
        {
            var wasEditing = EditingVersionId is not null;
            await using var database = NewDatabase();
            var input = new GameVersionInput(
                ManualGame, ManualVersionNumber, ManualVersionName,
                ParseRequiredActivityTime(ManualVersionStart, "版本开始时间", "Asia/Shanghai"),
                ParseRequiredActivityTime(ManualVersionEnd, "版本结束时间", "Asia/Shanghai"), ManualVersionNotes);
            var service = new ManualDataService(database);
            var result = EditingVersionId is Guid versionId
                ? await service.UpdateVersionAsync(versionId, input, false)
                : await service.SaveVersionAsync(input, false);
            if (result.HasOverlapWarning)
            {
                ImportMessage = "版本未保存：与同一游戏的已有版本时间重叠，请先调整时间。";
                return;
            }

            await new EndgameScheduleMaintenanceService(database)
                .RefreshVersionDependentRulesAsync(_calendarDate);
            ImportMessage = wasEditing
                ? "版本已更新，并已重算该版本相关周期玩法。"
                : "版本已保存为人工确认的正式数据，并已重算版本相关周期玩法。";
            ResetVersionEditor();
            LoadManualData();
            RefreshActivities();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ImportMessage = "版本未保存：" + ex.Message;
        }
    }

    [RelayCommand]
    private void BeginEndgameReanchor(Guid ruleId)
        => ImportMessage = EndgameRules.SingleOrDefault(x => x.Id == ruleId) is { CanReanchor: true }
            ? "重新锚定入口已打开；保存会只重算当前规则的当前和后两期。"
            : EndgameRules.Any(x => x.Id == ruleId)
                ? "该规则随版本变化，不能单独重新锚定；请先到版本管理录入版本时间。"
            : "未找到要重新锚定的周期规则。";

    [RelayCommand]
    private void BeginEndgameOverride(Guid occurrenceId)
        => ImportMessage = EndgameOccurrences.Any(x => x.Id == occurrenceId)
            ? "单期覆盖入口已打开；覆盖不会影响其他周期规则。"
            : "未找到要覆盖的周期记录。";

    [RelayCommand]
    private void SelectEndgameRule(Guid ruleId)
    {
        if (EndgameRules.SingleOrDefault(x => x.Id == ruleId) is { CanReanchor: false })
        {
            ImportMessage = "该规则随版本变化，不能单独重新锚定；请到版本管理录入版本时间。";
            return;
        }

        EndgameRuleToAdjustId = ruleId;
        EndgameOccurrenceToAdjustId = null;
        // Provide a valid editable default so the save action remains usable even when
        // Windows UI automation cannot commit an empty TextBox value on first focus.
        EndgameAnchorDate = _calendarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ImportMessage = "\u8bf7\u8f93\u5165\u65b0\u951a\u70b9\u65e5\u671f\uff08yyyy-MM-dd\uff09\u5e76\u4fdd\u5b58\u3002";
        EndgameReanchorRequested?.Invoke();
    }

    [RelayCommand]
    private void SelectEndgameOccurrence(Guid occurrenceId)
    {
        var occurrence = EndgameOccurrences.SingleOrDefault(x => x.Id == occurrenceId);
        if (occurrence is null) return;
        using var database = NewDatabase();
        var storedOccurrence = database.EndgameOccurrences.AsNoTracking().SingleOrDefault(x => x.Id == occurrenceId);
        EndgameRuleToAdjustId = occurrence.RuleId;
        EndgameOccurrenceToAdjustId = occurrenceId;
        // Override fields are DateOnly values; occurrence display text may include
        // the exact refresh time (for example, "04:00") and must not leak into
        // DateOnly parsing.
        var occurrenceDate = occurrence.DisplayStart.Length >= 10
            ? occurrence.DisplayStart[..10]
            : occurrence.DisplayStart;
        EndgameOverrideScheduledDate = storedOccurrence?.ScheduledDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? occurrenceDate;
        EndgameOverrideDate = occurrenceDate;
        EndgameOverrideStartTime = string.Empty;
        EndgameOverrideEndTime = string.Empty;
        EndgameOverrideNotes = string.Empty;
        ImportMessage = "\u53ef\u540c\u65f6\u8c03\u6574\u65e5\u671f\u4e0e\u65f6\u95f4\uff08HH:mm\uff09\uff0c\u4fdd\u5b58\u540e\u6309\u8c03\u6574\u540e\u7684\u5468\u671f\u7ee7\u7eed\u8ba1\u7b97\u3002";
        EndgameOverrideRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ApplyEndgameReanchorAsync()
    {
        if (EndgameRuleToAdjustId is not { } ruleId)
        {
            ImportMessage = "\u8bf7\u5148\u9009\u62e9\u8981\u91cd\u65b0\u951a\u5b9a\u7684\u5468\u671f\u89c4\u5219\u3002";
            return;
        }

        if (EndgameRules.SingleOrDefault(x => x.Id == ruleId) is { CanReanchor: false })
        {
            ImportMessage = "该规则随版本变化，不能单独重新锚定；请到版本管理录入版本时间。";
            return;
        }

        try
        {
            await using var database = NewDatabase();
            var anchorText = string.IsNullOrWhiteSpace(EndgameAnchorDate)
                ? _calendarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : EndgameAnchorDate;
            var anchor = DateOnly.Parse(anchorText, CultureInfo.InvariantCulture);
            await new EndgameScheduleMaintenanceService(database).ReanchorAsync(ruleId, anchor, _calendarDate);
            ImportMessage = "\u5468\u671f\u951a\u70b9\u5df2\u4fdd\u5b58\uff0c\u4ec5\u5f71\u54cd\u5f53\u524d\u89c4\u5219\u3002";
            LoadManualData();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            ImportMessage = "\u91cd\u65b0\u951a\u5b9a\u5931\u8d25\uff1a" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ApplyEndgameOverrideAsync()
    {
        if (EndgameRuleToAdjustId is not { } ruleId)
        {
            ImportMessage = "\u8bf7\u5148\u9009\u62e9\u8981\u8986\u76d6\u7684\u5468\u671f\u8bb0\u5f55\u3002";
            return;
        }

        try
        {
            await using var database = NewDatabase();
            var scheduledText = string.IsNullOrWhiteSpace(EndgameOverrideScheduledDate)
                ? _calendarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : EndgameOverrideScheduledDate;
            var startsOnText = string.IsNullOrWhiteSpace(EndgameOverrideDate)
                ? scheduledText
                : EndgameOverrideDate;
            var scheduled = DateOnly.Parse(scheduledText, CultureInfo.InvariantCulture);
            var startsOn = DateOnly.Parse(startsOnText, CultureInfo.InvariantCulture);
            var startTime = ParseOptionalTime(EndgameOverrideStartTime, "开始时间");
            var endTime = ParseOptionalTime(EndgameOverrideEndTime, "结束时间");
            await new EndgameScheduleMaintenanceService(database).OverrideAsync(ruleId,
                new EndgameOccurrenceOverride(scheduled, startsOn, startTime, Notes: EndgameOverrideNotes, EndTime: endTime), _calendarDate);
            ImportMessage = "\u5355\u671f\u8c03\u6574\u5df2\u4fdd\u5b58\uff0c\u540e\u7eed\u5468\u671f\u5df2\u6309\u8c03\u6574\u540e\u7684\u65e5\u671f\u548c\u89c4\u5f8b\u91cd\u65b0\u8ba1\u7b97\u3002";
            LoadManualData();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            ImportMessage = "\u5355\u671f\u8986\u76d6\u5931\u8d25\uff1a" + ex.Message;
        }
    }

    private static TimeOnly? ParseOptionalTime(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) return result;
        throw new FormatException($"{label}必须使用 HH:mm 格式。");
    }

    [RelayCommand]
    private async Task ImportCalendarDataAsync()
    {
        try
        {
            await using var importDatabase = NewDatabase();
            _activeImportPreview = await new QimiaoImportService(new DbContextQimiaoImportStore(importDatabase)).PreviewAsync(ImportJsonText);
            ImportPreviewEntries.Clear();
            foreach (var entry in _activeImportPreview.Entries)
                ImportPreviewEntries.Add(new ImportPreviewCard(entry.SelectionKey, entry.RecordType, entry.ChangeKind.ToString(), entry.NaturalKey, entry.ChangeKind != QimiaoImportChangeKind.Duplicate));
            ImportMessage = $"导入预览已生成：{ImportPreviewEntries.Count} 项；请选择后确认导入。";
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            ImportPreviewEntries.Clear();
            ImportMessage = "导入预览失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        if (_activeImportPreview is null)
        {
            ImportMessage = "请先生成导入预览。";
            return;
        }

        try
        {
            var selected = ImportPreviewEntries.Where(x => x.IsSelected).Select(x => x.SelectionKey).ToArray();
            await using var database = NewDatabase();
            var count = await new QimiaoImportService(new DbContextQimiaoImportStore(database)).ConfirmAsync(_activeImportPreview, selected);
            ImportMessage = $"已确认导入 {count} 项正式数据。";
            _activeImportPreview = null;
            ImportPreviewEntries.Clear();
            LoadManualData();
            LoadCalendar();
            RefreshActivities();
        }
        catch (Exception ex)
        {
            ImportMessage = "确认导入失败：" + ex.Message;
        }
    }

    private QimiaoDailyDbContext NewDatabase() => new(new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite(_connectionString).Options);

    private static DateTimeOffset ParseRequiredActivityTime(string text, string field, string timezone, TimeOnly? dateOnlyDefault = null)
         => ParseOptionalActivityTime(text, field, timezone, dateOnlyDefault) ?? throw new FormatException($"{field}不能为空。");

    private static DateTimeOffset? ParseOptionalActivityTime(string text, string field, string timezone, TimeOnly? dateOnlyDefault = null)
    {
        var value = text.Trim();
        if (value.Length == 0) return null;

        if (HasExplicitOffset(value))
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var offsetValue)) return offsetValue;
            if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out offsetValue)) return offsetValue;
            throw new FormatException($"{field}必须是有效的 ISO 时间或本地时间。");
        }

        if (string.IsNullOrWhiteSpace(timezone)) throw new FormatException($"{field}没有时区，无法标准化。");
        DateTime localTime;
        if (dateOnlyDefault is { } defaultTime && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            localTime = dateOnly.ToDateTime(defaultTime);
        }
        else if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out localTime) &&
                 !DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out localTime))
            throw new FormatException($"{field}必须是有效的 ISO 时间或本地时间。");

        var timeZone = ResolveActivityTimezone(timezone);
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified));
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return true;
        var index = Math.Max(value.LastIndexOf('+'), value.LastIndexOf('-'));
        var suffixLength = value.Length - index;
        return index > 10 && (suffixLength == 5 || suffixLength == 6);
    }

    private static TimeZoneInfo ResolveActivityTimezone(string value)
    {
        var normalized = value.Trim();
        if (normalized.Equals("Asia/Shanghai", StringComparison.OrdinalIgnoreCase)) normalized = "China Standard Time";
        if (normalized.Equals("UTC+08:00", StringComparison.OrdinalIgnoreCase)) normalized = "China Standard Time";
        try { return TimeZoneInfo.FindSystemTimeZoneById(normalized); }
        catch (TimeZoneNotFoundException)
        {
            var match = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(x => x.Id.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
            return match ?? throw new FormatException($"无法识别时区：{value}。");
        }
    }

    private void LoadManualData()
    {
        using var database = NewDatabase();
        ManualEvents.Clear();
        ManualBanners.Clear();
        ManualVersions.Clear();
        EndgameRules.Clear();
        EndgameOccurrences.Clear();

        foreach (var item in database.ManualEvents.AsNoTracking().ToList().OrderByDescending(x => x.StartAt))
            ManualEvents.Add(ManualDataCardMapper.ToCard(item));
        foreach (var item in database.Banners.AsNoTracking().Include(x => x.Characters).ToList().OrderByDescending(x => x.StartAt))
            ManualBanners.Add(ManualDataCardMapper.ToCard(item));
        foreach (var item in database.GameVersions.AsNoTracking().ToList().OrderByDescending(x => x.StartAt))
            ManualVersions.Add(ManualDataCardMapper.ToCard(item));

        var anchors = database.EndgameAnchors.AsNoTracking().ToList()
            .GroupBy(x => x.RuleId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(anchor => anchor.StartsAt).First());
        var rules = database.EndgameRules.AsNoTracking().ToList();
        var ruleNames = rules.ToDictionary(x => x.Id, x => x.Name);
        foreach (var rule in rules.OrderBy(x => x.Game).ThenBy(x => x.Name))
        {
            var anchorText = anchors.TryGetValue(rule.Id, out var anchor)
                ? $"锚点：{anchor.StartsAt:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(anchor.Notes) ? string.Empty : $"（{anchor.Notes}）")
                : "尚未设置锚点";
            EndgameRules.Add(new EndgameRuleCard(rule.Id, rule.Game, rule.Name, rule.RuleKind, rule.ConfigurationJson, rule.Enabled, anchorText,
                !rule.RuleKind.StartsWith("VERSION_", StringComparison.OrdinalIgnoreCase)));
        }

        var today = DateTimeOffset.UtcNow.Date;
        foreach (var group in database.EndgameOccurrences.AsNoTracking().ToList()
                     .Where(x => x.StartAt >= today)
                     .GroupBy(x => x.RuleId))
        {
            var configuration = rules.SingleOrDefault(x => x.Id == group.Key)?.ConfigurationJson;
            var dateOnly = configuration?.Contains("DATE_ONLY", StringComparison.OrdinalIgnoreCase) == true
                || configuration?.Contains("DateOnly", StringComparison.OrdinalIgnoreCase) == true;
            var ruleName = ruleNames.TryGetValue(group.Key, out var name) ? name : "未知规则";
            foreach (var occurrence in group.OrderBy(x => x.StartAt).Take(3))
                EndgameOccurrences.Add(ManualDataCardMapper.ToCard(occurrence, ruleName, dateOnly));
        }
    }

    private void LoadActivities()
    {
        using var database = NewDatabase();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-3);
        var items = database.TimelineItems.Include(x => x.Evidence)
            .Where(x => x.ReviewStatus == ReviewStatus.Pending || x.ReviewStatus == ReviewStatus.Confirmed)
            .ToList()
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CanonicalIdentity) ? x.Id.ToString() : x.CanonicalIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(item => item.FetchedAt).First())
            .Where(x => x.EndAt is null || x.EndAt >= cutoff);
        if (GameFilter != "ALL") items = items.Where(x => string.Equals(x.GameCode, GameFilter, StringComparison.OrdinalIgnoreCase));
        if (TypeFilter != "ALL") items = items.Where(x => string.Equals(x.ItemType, TypeFilter, StringComparison.OrdinalIgnoreCase));
        items = items.OrderBy(x => x.NormalizedTime);
        foreach (var item in items) (item.ReviewStatus == ReviewStatus.Confirmed ? ConfirmedActivities : PendingActivities).Add(ToCard(item));

        // The right-hand "stored game activities" list is the authoritative V3
        // workspace.  Legacy calendar timeline rows are retired after migration,
        // so reading only calendar-game:* here makes the list appear empty even
        // though formal activities, banners and calculated endgame rows exist.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var now = DateTimeOffset.UtcNow;
        foreach (var item in database.ManualEvents.AsNoTracking()
                     .Where(x => !x.Archived && x.UserConfirmed)
                     .ToList()
                     .Where(x => GameFilter == "ALL" || string.Equals(x.Game, GameFilter, StringComparison.OrdinalIgnoreCase))
                     .Where(x => TypeFilter == "ALL" || TypeFilter == "EVENT")
                     .OrderBy(x => x.StartAt))
            StoredGameActivities.Add(ToFormalCard(item.Id, item.Game, "EVENT", item.Name, item.StartAt, item.EndAt, item.Notes, now, zone));

        foreach (var item in database.Banners.AsNoTracking()
                     .Include(x => x.Characters)
                     .Where(x => !x.Archived && x.UserConfirmed)
                     .ToList()
                     .Where(x => GameFilter == "ALL" || string.Equals(x.Game, GameFilter, StringComparison.OrdinalIgnoreCase))
                     .Where(x => TypeFilter == "ALL" || TypeFilter == "GACHA")
                     .OrderBy(x => x.StartAt))
        {
            var characters = string.Join("、", item.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name));
            var title = string.IsNullOrWhiteSpace(characters) ? item.Name : $"{item.Name}（{characters}）";
            StoredGameActivities.Add(ToFormalCard(item.Id, item.Game, "GACHA", title, item.StartAt, item.EndAt,
                string.IsNullOrWhiteSpace(item.CustomType) ? item.Type : item.CustomType!, now, zone,
                item.Type, item.CustomType));
        }

        var enabledRules = database.EndgameRules.AsNoTracking().Where(x => x.Enabled).ToList();
        var ruleNames = enabledRules.ToDictionary(x => x.Id, x => x.Name);
        foreach (var item in database.EndgameOccurrences.AsNoTracking()
                     .Where(x => enabledRules.Select(rule => rule.Id).Contains(x.RuleId))
                     .ToList()
                     .Where(x => GameFilter == "ALL" || string.Equals(enabledRules.Single(rule => rule.Id == x.RuleId).Game, GameFilter, StringComparison.OrdinalIgnoreCase))
                     .Where(x => TypeFilter == "ALL" || TypeFilter == "ENDGAME")
                     .OrderBy(x => x.StartAt))
        {
            var rule = enabledRules.Single(rule => rule.Id == item.RuleId);
            var dateOnly = string.Equals(item.TimePrecision, "DATE_ONLY", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.TimePrecision, "DATE_ONLY", StringComparison.OrdinalIgnoreCase);
            var start = dateOnly && item.OccurrenceDate is { } date
                ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8))
                : item.StartAt;
            var end = dateOnly && item.OccurrenceDate is { } occurrenceDate
                ? new DateTimeOffset((item.EndAt == default ? occurrenceDate : DateOnly.FromDateTime(item.EndAt.DateTime)).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8))
                : item.EndAt;
            StoredGameActivities.Add(ToFormalCard(item.Id, rule.Game, "ENDGAME", ruleNames[item.RuleId], start, end,
                dateOnly ? "DATE_ONLY" : item.Notes, now, zone));
        }

        var archived = database.TimelineItems.Include(x => x.Evidence)
            .Where(x => x.ReviewStatus == ReviewStatus.Archived)
            .ToList()
            .Where(x => GameFilter == "ALL" || string.Equals(x.GameCode, GameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(x => TypeFilter == "ALL" || string.Equals(x.ItemType, TypeFilter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FetchedAt);
        foreach (var item in archived) ArchivedActivities.Add(ToCard(item));
    }

    private void RefreshActivities()
    {
        PendingActivities.Clear(); ConfirmedActivities.Clear(); ArchivedActivities.Clear(); StoredGameActivities.Clear(); LoadActivities();
        PendingReviewCount = PendingActivities.Count; ConfirmedCount = ConfirmedActivities.Count;
    }

    private void LoadArtworks()
    {
        using var database = NewDatabase();
        var items = database.Artworks.Where(x => x.ReviewStatus == ReviewStatus.Pending || x.ReviewStatus == ReviewStatus.Confirmed).ToList().OrderByDescending(x => x.PublishedAt);
        foreach (var item in items) (item.ReviewStatus == ReviewStatus.Confirmed ? ConfirmedArtworks : PendingArtworks).Add(ToArtworkCard(item));
    }

    private void RefreshArtworks()
    {
        PendingArtworks.Clear(); ConfirmedArtworks.Clear(); LoadArtworks(); LoadArtworkRun(); PendingArtworkCount = PendingArtworks.Count;
    }

    private void LoadArtworkRun()
    {
        using var database = NewDatabase();
        var run = database.ArtworkDailyRuns.AsNoTracking().ToList().OrderByDescending(x => x.CompletedAt).FirstOrDefault();
        if (run is null)
        {
            ArtworkRunCountText = "0/30";
            ArtworkRunStatus = "NOT_RUN";
            ArtworkRunMessage = "尚未执行美图每日采集。";
            ArtworkRunTimeText = "-";
            return;
        }

        ArtworkRunCountText = $"{run.NewCandidateCount}/{run.TargetCount}";
        ArtworkRunStatus = run.Status;
        ArtworkRunMessage = string.IsNullOrWhiteSpace(run.FailureReason) ? "本次采集已完成。" : DisplayNameMapper.ProviderError(run.FailureReason);
        var china = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(run.CompletedAt, "China Standard Time");
        ArtworkRunTimeText = china.ToString("MM-dd HH:mm");
    }

    private void LoadCalendar()
    {
        using var database = NewDatabase();
        var service = new CalendarService(database);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").Date);
        var year = service.ForYearAsync(today.Year).GetAwaiter().GetResult();
        static string Details(IReadOnlyList<CalendarOccurrence> items) => string.Join(" · ", items.Select(x =>
        {
            if (x.Detail is null) return x.Title;
            var detail = string.Equals(x.Kind, "BIRTHDAY", StringComparison.OrdinalIgnoreCase)
                ? DisplayNameMapper.Game(x.Detail)
                : x.Detail;
            return x.Title + " (" + detail + ")";
        }));
        MonthCalendarDays.Clear(); YearCalendarDays.Clear(); CalendarEvents.Clear(); _allYearCalendarDays.Clear();
        foreach (var date in year.Keys)
        {
            var occurrences = year[date];
            var card = new CalendarDayCard(
                date.ToDateTime(TimeOnly.MinValue),
                $"{date.Month}.{date.Day}",
                Details(occurrences),
                string.Join("|", occurrences.Select(x => x.Kind).Distinct(StringComparer.OrdinalIgnoreCase)),
                string.Join("|", occurrences.Where(x => x.Kind == "BIRTHDAY").Select(x => x.Detail).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)));
            _allYearCalendarDays.Add(card);
            if (date.Month == today.Month) MonthCalendarDays.Add(card with { DayNumber = date.Day.ToString() });
        }
        foreach (var item in database.CalendarEvents.AsNoTracking().Where(x => x.Enabled && x.Kind != "GAME").OrderBy(x => x.EventDate).ToList()) CalendarEvents.Add(new() { Id = item.Id, Date = item.EventDate.ToDateTime(TimeOnly.MinValue), Kind = item.Kind, Title = item.Title, Detail = item.Detail ?? string.Empty });
        Anniversaries.Clear();
        foreach (var item in database.Anniversaries.AsNoTracking().OrderBy(x => x.StartedOn).ThenBy(x => x.Title))
            Anniversaries.Add(ManualDataCardMapper.ToCard(item));
        BirthdayCandidates.Clear();
        foreach (var item in database.Birthdays.AsNoTracking().OrderBy(x => x.Month == 0 ? 13 : x.Month).ThenBy(x => x.Day).ThenBy(x => x.Franchise).ThenBy(x => x.Character))
            BirthdayCandidates.Add(ToBirthdayCard(item));
        EnabledCalendarCount = database.Birthdays.Count(x => x.Enabled);
        ApplyCalendarFilters();
        SelectCalendarDate(_calendarDate.ToDateTime(TimeOnly.MinValue));
    }

    private void ApplyCalendarFilters()
    {
        if (_allYearCalendarDays.Count == 0) return;
        var search = CalendarSearchText.Trim();
        var kind = CalendarKindFilter;
        var franchise = CalendarFranchiseFilter;
        YearCalendarDays.Clear();
        foreach (var card in _allYearCalendarDays)
        {
            if (search.Length > 0 && !($"{card.DayNumber} {card.Details} {card.KindText} {card.FranchiseText}").Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            if (kind != "全部" && !card.KindText.Split('|', StringSplitOptions.RemoveEmptyEntries).Contains(kind, StringComparer.OrdinalIgnoreCase)) continue;
            if (franchise != "全部" && !card.FranchiseText.Split('|', StringSplitOptions.RemoveEmptyEntries).Contains(franchise, StringComparer.OrdinalIgnoreCase)) continue;
            YearCalendarDays.Add(card);
        }
    }

    private DateOnly CurrentReportDate()
    {
        if (DateTime.TryParse(Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_DATE"), out var captureDate)) return DateOnly.FromDateTime(captureDate);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").Date);
    }

    private void LoadBgiCommits()
    {
        using var database = NewDatabase();
        var repositories = SourceSettings.Load(_paths).BgiRepositories;
        var primaryRepository = repositories.FirstOrDefault();
        var secondaryRepository = repositories.Skip(1).FirstOrDefault();
        BgiCommits.Clear(); BgiMainCommits.Clear(); BgiScriptsCommits.Clear();
        var now = DateTimeOffset.UtcNow;
        var (start, end) = GitHubCommitProvider.DailyWindow(now);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var localStart = TimeZoneInfo.ConvertTime(start, timezone);
        var localEnd = TimeZoneInfo.ConvertTime(end, timezone);
        BgiWindowText = $"Asia/Shanghai · [{localStart:MM-dd HH:mm}, {localEnd:MM-dd HH:mm})";
        BgiWindowIncomplete = GitHubCommitProvider.IsWindowIncomplete(now);
        BgiWindowStatus = BgiWindowIncomplete ? "COMMIT WINDOW INCOMPLETE" : "WINDOW COMPLETE";
        BgiWindowStatusDisplay = BgiWindowIncomplete ? "提交窗口未完成" : "提交窗口完整";
        foreach (var item in database.GitCommitRecords.AsNoTracking().ToList().OrderByDescending(x => x.CommitterDate ?? x.AuthorDate))
        {
            var card = ToBgiCard(item, timezone);
            BgiCommits.Add(card);
            if (string.Equals(item.Repository, primaryRepository, StringComparison.OrdinalIgnoreCase)) BgiMainCommits.Add(card);
            else if (string.Equals(item.Repository, secondaryRepository, StringComparison.OrdinalIgnoreCase)) BgiScriptsCommits.Add(card);
        }
    }

    private void LoadReportWorkspace()
    {
        LoadBgiCommits();
        using var database = NewDatabase();
        var service = new DailyReportService(database);
        Task.Run(() => service.BuildAutomaticSectionsAsync(CurrentReportDate())).GetAwaiter().GetResult();
        var draft = Task.Run(() => service.GetOrCreateAsync(CurrentReportDate())).GetAwaiter().GetResult();
        ReportSections.Clear();
        DeletedReportSections.Clear();
        foreach (var section in draft.Sections.OrderBy(x => x.SortOrder))
        {
            var card = new ReportSectionCard { Key = section.Key, Text = section.Text, Dirty = section.Dirty || section.ManualOverride, ManualOverride = section.ManualOverride, IsDeleted = section.IsDeleted };
            (section.IsDeleted ? DeletedReportSections : ReportSections).Add(card);
        }
        ComposedReport = Task.Run(() => service.ComposeAsync(CurrentReportDate())).GetAwaiter().GetResult();
    }

    private void LoadOperations()
    {
        using var database = NewDatabase();
        LoadArtworkRun();
        var service = new OperationsService(database);
        ProviderHealth.Clear();
        foreach (var item in service.GetHealthAsync().GetAwaiter().GetResult())
        {
            var details = $"最近成功：{item.LastSuccessAt?.ToString("MM-dd HH:mm") ?? "-"} · 最近失败：{item.LastFailureAt?.ToString("MM-dd HH:mm") ?? "-"} · 失败次数：{item.FailureCount}";
            var error = DisplayNameMapper.ProviderError(item.LastError);
            ProviderHealth.Add(new() { ProviderName = DisplayNameMapper.Provider(item.ProviderName), Status = DisplayNameMapper.ProviderStatus(item.Status), LastSuccess = item.LastSuccessAt?.ToString("MM-dd HH:mm") ?? "-", LastFailure = item.LastFailureAt?.ToString("MM-dd HH:mm") ?? "-", Latency = item.LastLatencyMs + " 毫秒", ItemCount = item.ItemCount, ParserStatus = DisplayNameMapper.ParserStatus(item.ParserStatus), FailureCount = item.FailureCount, Error = string.IsNullOrWhiteSpace(error) ? details : error + Environment.NewLine + details });
        }
        SchedulerTasks.Clear();
        foreach (var item in service.GetTasksAsync().GetAwaiter().GetResult()) SchedulerTasks.Add(new() { TaskKey = item.TaskKey, DisplayName = DisplayNameMapper.Task(item.TaskKey), ScheduleText = item.ScheduleText, Status = DisplayNameMapper.ProviderStatus(item.Status), LastRun = item.LastRunAt?.ToString("MM-dd HH:mm") ?? "-", FailureCount = item.FailureCount, Error = DisplayNameMapper.ProviderError(item.LastError) });
    }

    private static async Task<int> ExecuteSchedulerTaskAsync(QimiaoDailyDbContext database, string taskKey, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        return await new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths()).ExecuteAsync(taskKey, cancellationToken);
    }

    private static ArtworkCard ToArtworkCard(ArtworkEntity item)
    {
        return new(item.Id, item.Title, item.Author, DisplayNameMapper.ArtworkPlatform(item.Platform), item.SourceUrl, item.ThumbnailUrl, item.PublishedAt.ToString("yyyy-MM-dd HH:mm"), item.SelectedForReport, item.CharacterName, DisplayNameMapper.Game(item.FranchiseName), DisplayNameMapper.ArtworkCategory(item.Category), DisplayNameMapper.ArtworkTags(item.Tags), item.Width, item.Height, item.PerceptualHash);
    }

    private static BgiCommitCard ToBgiCard(GitCommitRecord item, TimeZoneInfo timezone)
    {
        var when = item.CommitterDate ?? item.AuthorDate ?? item.FetchedAt;
        var local = TimeZoneInfo.ConvertTime(when, timezone);
        var shortSha = item.Sha[..Math.Min(7, item.Sha.Length)];
        return new BgiCommitCard
        {
            Id = item.Id,
            Repository = item.Repository,
            Subject = item.Subject,
            Sha = shortSha,
            TimeText = local.ToString("MM-dd HH:mm"),
            PullRequestText = item.PullRequestNumber is null ? "无 PR" : $"PR #{item.PullRequestNumber}",
            Url = item.Url
        };
    }

    private static BirthdayCandidateCard ToBirthdayCard(BirthdayEntity item)
    {
        var date = BirthdayReviewService.CanEnable(item) ? $"{item.Month:00}-{item.Day:00}" : "生日未知";
        return new BirthdayCandidateCard
        {
            Id = item.Id,
            Character = DisplayNameMapper.BirthdayCharacter(
                string.IsNullOrWhiteSpace(item.CanonicalCharacterNameZhCn) ? item.Character : item.CanonicalCharacterNameZhCn,
                item.Aliases),
            Franchise = item.Franchise,
            DateText = date,
            StatusText = DisplayNameMapper.Verification(item.VerificationStatus),
            EnabledText = item.Enabled ? "已启用日报" : "未启用日报",
            Source = item.Source,
            SourceUrl = item.SourceUrl,
            Evidence = item.Evidence,
            Enabled = item.Enabled,
            CanEnable = BirthdayReviewService.CanEnable(item),
            Verification = item.VerificationStatus
        };
    }

    private static GameActivityCard ToFormalCard(
        Guid id,
        string game,
        string type,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string notes,
        DateTimeOffset now,
        TimeZoneInfo zone,
        string? gachaPoolKind = null,
        string? gachaPoolPhase = null)
    {
        var localStart = TimeZoneInfo.ConvertTime(startAt, zone);
        var localEnd = TimeZoneInfo.ConvertTime(endAt, zone);
        var dateOnly = type == "ENDGAME" && string.Equals(notes, "DATE_ONLY", StringComparison.OrdinalIgnoreCase);
        var timeText = dateOnly
            ? $"{localStart:yyyy-MM-dd} ~ {localEnd:yyyy-MM-dd}"
            : $"{localStart:yyyy-MM-dd HH:mm} ~ {localEnd:yyyy-MM-dd HH:mm}";
        var remaining = type == "ENDGAME" && string.Equals(notes, "DATE_ONLY", StringComparison.OrdinalIgnoreCase)
            ? $"至 {TimeZoneInfo.ConvertTime(endAt, zone):yyyy-MM-dd}"
            : TimeDisplay.Format(startAt, endAt, now, zone);
        var evidence = type switch
        {
            "EVENT" => "人工/导入活动",
            "GACHA" => "人工/导入卡池",
            _ => "程序按规则计算"
        };
        var summary = string.IsNullOrWhiteSpace(notes) || notes == "DATE_ONLY" ? evidence : notes;
        return new GameActivityCard(id, game, type, title, timeText, remaining,
            "暂无外部证据", summary, "Manual", startAt, endAt, gachaPoolKind, gachaPoolPhase);
    }

    private static GameActivityCard ToCard(TimelineItem item)
    {
        DateTimeOffset? china = item.NormalizedTime is null ? null : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.NormalizedTime.Value, "China Standard Time");
        var isDateOnly = item.TimePrecision == TimePrecision.DateOnly;
        var time = isDateOnly
            ? FormatDateRange(item.SourceTime, item.EndExpression)
            : china?.ToString("MM-dd HH:mm") ?? item.SourceTime ?? "日期待确认";
        var remaining = isDateOnly
            ? FormatDateOnlyRemaining(item.SourceTime, item.EndExpression)
            : TimeDisplay.Format(item.NormalizedTime, item.EndAt, DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
        var evidence = item.Evidence.FirstOrDefault();
        var change = ChangeLabel(item.ChangeKind);
        var summary = evidence is null ? $"Change: {change}" : $"Change: {change}" + Environment.NewLine + "Verification: " + item.VerificationStatus + Environment.NewLine + string.Join(Environment.NewLine, item.Evidence.Select(x => x.SourceProvider + " · " + x.SourceUrl + "\n" + x.SourceText));
        return new(item.Id, item.GameCode, item.ItemType + " · " + change, item.Title, time, remaining, evidence?.SourceUrl ?? "暂无证据", summary, item.VerificationStatus.ToString(), item.NormalizedTime, item.EndAt, item.GachaPoolKind, item.GachaPoolPhase);
    }

    private static string FormatDateRange(string? start, string? end)
        => string.IsNullOrWhiteSpace(end) ? start ?? "日期待确认" : $"{start ?? "日期待确认"} ~ {end}";

    private static string FormatDateOnlyRemaining(string? start, string? end)
    {
        if (!DateOnly.TryParse(end, out var endDate)) return "结束日期待确认";
        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time");
        var today = DateOnly.FromDateTime(now.DateTime);
        if (endDate < today) return "已结束";
        if (DateOnly.TryParse(start, out var startDate) && startDate > today) return $"{(startDate.DayNumber - today.DayNumber)}天后开始";
        return $"进行中，剩余 {endDate.DayNumber - today.DayNumber}天";
    }

    private static string ChangeLabel(TimelineChangeKind kind) => kind switch
    {
        TimelineChangeKind.New => "NEW",
        TimelineChangeKind.TimeChanged => "TIME_CHANGED",
        TimelineChangeKind.ContentChanged => "CONTENT_CHANGED",
        TimelineChangeKind.SourceChanged => "SOURCE_CHANGED",
        TimelineChangeKind.Conflict => "CONFLICT",
        _ => "NONE"
    };
}
