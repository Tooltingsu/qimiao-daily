using QimiaoDaily.Desktop.ViewModels;
using QimiaoDaily.Data;

namespace QimiaoDaily.Desktop.Tests;

public sealed class ManualDataUiTests
{
    [Fact]
    public void GamePage_ContainsManualActivitiesBannersEndgameVersionsAndImportEntry()
    {
        var xaml = File.ReadAllText(MainWindowPath);

        Assert.Contains("<TabItem Header=\"活动\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"卡池\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"周期玩法\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"版本管理\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"新增活动\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"新增卡池\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"新增版本\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"导入活动 JSON\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"立即刷新游戏数据\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellViewModel_ExposesManualWorkspaceCommandsAndCollections()
    {
        var viewModelType = typeof(ShellViewModel);

        foreach (var member in new[]
        {
            "ManualEvents", "ManualBanners", "ManualVersions", "EndgameRules", "EndgameOccurrences", "ImportPreviewEntries",
            "BeginManualEventCreateCommand", "BeginBannerCreateCommand", "BeginVersionCreateCommand", "BeginEndgameReanchorCommand", "BeginEndgameOverrideCommand", "ImportCalendarDataCommand", "EditStoredGameActivityCommand", "DeleteStoredGameActivityCommand", "DeleteManualBannerCommand", "DeleteManualVersionCommand", "GameActivityTabIndex"
        })
            Assert.NotNull(viewModelType.GetProperty(member));
        Assert.NotNull(viewModelType.GetProperty("IsImportPanelOpen"));
        Assert.NotNull(viewModelType.GetProperty("CloseImportPanelCommand"));
    }

    [Fact]
    public void StoredGameList_CanEditEverySupportedCategory()
    {
        var xaml = File.ReadAllText(MainWindowPath);

        Assert.Contains("EditStoredGameActivityCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteStoredGameActivityCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteManualBannerCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteManualVersionCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding GameActivityTabIndex}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DataTrigger Binding=\"{Binding Type}\" Value=\"EVENT\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GamePage_DateOnlyEndgameDoesNotBindFabricatedTime()
    {
        var xaml = File.ReadAllText(MainWindowPath);

        Assert.Contains("Text=\"{Binding DisplayStart}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding StartAt,StringFormat=时间：{0:HH:mm}}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportPreview_ShowsChangeKindAndConfirmsSelectedEntries()
    {
        var xaml = ReadDesktopFile("ImportEditorWindow.xaml");

        Assert.Contains("Text=\"{Binding Change}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Confirm_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"生成预览\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsImportPanelOpen", File.ReadAllText(MainWindowPath), StringComparison.Ordinal);
    }

    [Fact]
    public void BannerEditor_UsesStructuredCharacterRows()
    {
        var xaml = ReadDesktopFile("BannerEditorWindow.xaml");

        Assert.Contains("ItemsSource=\"{Binding BannerCharacterEditors}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ManualGameOptions", xaml, StringComparison.Ordinal);
        Assert.Contains("ManualBannerTypeOptions", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ManualBannerType,UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AddBannerCharacterCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("RemoveBannerCharacterCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivityEditor_UsesGameSelectorAndDoesNotAppendDuplicateEditButtons()
    {
        var codeBehind = File.ReadAllText(Path.Combine(Path.GetDirectoryName(MainWindowPath)!, "MainWindow.xaml.cs"));
        var editor = ReadDesktopFile("ManualEventEditorWindow.xaml");

        Assert.Contains("ManualGameOptions", editor, StringComparison.Ordinal);
        Assert.Contains("ManualGame", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertManualEventGameSelector", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertManualEditButtons", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void BannerTypeOptions_DoNotContainWorkBannerTypo()
    {
        var desktopDirectory = Path.GetDirectoryName(MainWindowPath)!;
        var source = File.ReadAllText(Path.Combine(desktopDirectory, "ViewModels", "ShellViewModel.cs"));

        Assert.DoesNotContain("\\u4e0a\\u73ed\\u5361\\u6c60", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualEditors_ExplainPopupWorkflowAndTimeDefaults()
    {
        var main = File.ReadAllText(MainWindowPath);
        var activity = ReadDesktopFile("ManualEventEditorWindow.xaml");

        Assert.Contains("独立小窗口中填写", main, StringComparison.Ordinal);
        Assert.Contains("验证通过并保存后窗口自动关闭", main, StringComparison.Ordinal);
        Assert.Contains("开始时间仅填日期时默认 04:00", activity, StringComparison.Ordinal);
        Assert.Contains("结束时间仅填日期时默认 03:59", activity, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualEditors_PutVisibleLabelsInsideTheirPopupWindows()
    {
        var activity = ReadDesktopFile("ManualEventEditorWindow.xaml");
        var version = ReadDesktopFile("VersionEditorWindow.xaml");
        var codeBehind = ReadDesktopFile("MainWindow.xaml.cs");

        Assert.Contains("活动名称（日报显示名称）", activity, StringComparison.Ordinal);
        Assert.Contains("开始日期时间（Asia/Shanghai，yyyy-MM-dd HH:mm）", activity, StringComparison.Ordinal);
        Assert.Contains("版本号（例如 7.0、3.4）", version, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertManualFieldLabels", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void BannerAndVersionTabs_ContainFieldLabelsInsideTheirPanels()
    {
        var banner = ReadDesktopFile("BannerEditorWindow.xaml");
        var version = ReadDesktopFile("VersionEditorWindow.xaml");

        Assert.Contains("卡池名称（日报显示名称）", banner, StringComparison.Ordinal);
        Assert.Contains("ManualBannerTypeOptions", banner, StringComparison.Ordinal);
        Assert.Contains("角色（按显示顺序逐行填写）", banner, StringComparison.Ordinal);
        Assert.Contains("版本号（例如 7.0、3.4）", version, StringComparison.Ordinal);
        Assert.Contains("版本名称（日报显示名称）", version, StringComparison.Ordinal);
        Assert.Contains("开始日期时间（Asia/Shanghai，yyyy-MM-dd HH:mm）", version, StringComparison.Ordinal);
        Assert.Contains("结束日期时间（Asia/Shanghai，yyyy-MM-dd HH:mm）", version, StringComparison.Ordinal);
    }

    [Fact]
    public void CrudEditors_AreModalWindowsAndNotInlineForms()
    {
        var main = File.ReadAllText(MainWindowPath);
        var codeBehind = ReadDesktopFile("MainWindow.xaml.cs");
        var windows = new[]
        {
            "ManualEventEditorWindow", "BannerEditorWindow", "VersionEditorWindow",
            "EndgameReanchorWindow", "EndgameOverrideWindow", "ImportEditorWindow",
            "ActivityEditorWindow", "ArtworkEditorWindow", "BirthdayEditorWindow",
            "AnniversaryEditorWindow", "ReportSectionEditorWindow"
        };

        foreach (var window in windows)
        {
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(MainWindowPath)!, window + ".xaml")));
            Assert.Contains("new " + window, codeBehind, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Text=\"{Binding ManualEventName", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ManualBannerName", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ManualVersionNumber", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding EndgameAnchorDate", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding EndgameOverrideDate", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ImportJsonText", main, StringComparison.Ordinal);
    }

    [Fact]
    public void PopupSaveHandlers_CloseOnlyAfterSuccessfulSaveMessage()
    {
        foreach (var file in new[]
        {
            "ManualEventEditorWindow.xaml.cs", "BannerEditorWindow.xaml.cs", "VersionEditorWindow.xaml.cs",
            "EndgameReanchorWindow.xaml.cs", "EndgameOverrideWindow.xaml.cs", "ImportEditorWindow.xaml.cs",
            "ActivityEditorWindow.xaml.cs", "ArtworkEditorWindow.xaml.cs", "BirthdayEditorWindow.xaml.cs",
            "AnniversaryEditorWindow.xaml.cs", "ReportSectionEditorWindow.xaml.cs"
        })
        {
            var source = ReadDesktopFile(file);
            Assert.Contains("DialogResult = true", source, StringComparison.Ordinal);
            Assert.Contains("StartsWith", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PopupComboBoxes_UseDarkApplicationTemplateForSelectedAndDropdownItems()
    {
        var app = ReadDesktopFile("App.xaml");

        Assert.Contains("<Style TargetType=\"ComboBox\">", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"Background\" Value=\"#151A2F\"", app, StringComparison.Ordinal);
        Assert.Contains("Background=\"{TemplateBinding Background}\" BorderBrush=\"{TemplateBinding BorderBrush}\" Foreground=\"{TemplateBinding Foreground}\"", app, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"ComboBoxItem\">", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"Background\" Value=\"#1A203A\"", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"Foreground\" Value=\"White\"", app, StringComparison.Ordinal);

        foreach (var file in new[]
        {
            "ManualEventEditorWindow.xaml", "BannerEditorWindow.xaml", "VersionEditorWindow.xaml",
            "ActivityEditorWindow.xaml", "BirthdayEditorWindow.xaml"
        })
            Assert.DoesNotContain("<Style TargetType=\"ComboBox\">", ReadDesktopFile(file), StringComparison.Ordinal);
    }

    [Fact]
    public void ArtworkCards_ShowLocalCacheStateAndOfferDirectImageCopy()
    {
        var xaml = File.ReadAllText(MainWindowPath);
        var card = File.ReadAllText(Path.Combine(Path.GetDirectoryName(MainWindowPath)!, "ViewModels", "ArtworkCard.cs"));

        Assert.Contains("Content=\"复制图片\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.CopyArtworkImageCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding IsCachedLocally}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CacheStatusText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("public bool IsCachedLocally", card, StringComparison.Ordinal);
    }

    [Fact]
    public void GamePage_ShowsConfirmedDataOnRightAndRemovesSeparateBannerEditorTab()
    {
        var xaml = File.ReadAllText(MainWindowPath);

        Assert.DoesNotContain("<TabItem Header=\"卡池编辑\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding StoredGameActivitiesView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ManualBanners}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ManualVersions}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("已存游戏活动", xaml, StringComparison.Ordinal);
        Assert.Contains("已确认卡池", xaml, StringComparison.Ordinal);
        Assert.Contains("已确认版本", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DateOnlyOccurrenceCard_UsesAuthoritativeOccurrenceDate()
    {
        var mapper = typeof(ShellViewModel).Assembly.GetType("QimiaoDaily.Desktop.ViewModels.ManualDataCardMapper", throwOnError: true)!;
        var method = mapper.GetMethod("ToCard", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null, types: [typeof(EndgameOccurrenceEntity), typeof(string), typeof(bool)], modifiers: null)!;
        var occurrence = new EndgameOccurrenceEntity
        {
            OccurrenceDate = new DateOnly(2026, 9, 4),
            StartAt = new DateTimeOffset(2026, 9, 3, 16, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 9, 17, 16, 0, 0, TimeSpan.Zero)
        };

        var card = (EndgameOccurrenceCard)method.Invoke(null, [occurrence, "轨外之境", true])!;

        Assert.Equal("2026-09-04", card.DisplayStart);
        Assert.Equal("2026-09-04", card.DisplayEnd);
    }

    private static string MainWindowPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
                directory = directory.Parent;

            Assert.NotNull(directory);
            return Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml");
        }
    }

    private static string ReadDesktopFile(string name)
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(MainWindowPath)!, name));
}
