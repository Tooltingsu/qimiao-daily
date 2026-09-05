using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void ActivityRemainingRefresher_RecomputesWithoutReloadingData()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var now = DateTimeOffset.Parse("2026-08-17T10:00:00+08:00");
        var cards = new ObservableCollection<GameActivityCard>
        {
            new(Guid.NewGuid(), "GENSHIN", "EVENT", "活动", "08-17 11:00", "旧值", "https://example.invalid", "", "Unverified",
                now.AddHours(1), now.AddHours(23))
        };

        ActivityRemainingRefresher.Refresh(cards, now, zone);
        Assert.Equal("距开始 0天1小时", cards[0].RemainingText);

        ActivityRemainingRefresher.Refresh(cards, now.AddHours(2), zone);
        Assert.Equal("剩余 0天21小时", cards[0].RemainingText);
    }

    [Fact]
    public void ReportSectionCard_DoesNotLeakUnknownInternalKey()
    {
        var card = new ReportSectionCard { Key = "unexpected_internal_section" };

        Assert.Equal("其他日报内容", card.DisplayName);
    }

    [Fact]
    public void GameActivityCard_LocalizesEvidenceSummaryInternalLabels()
    {
        var card = new GameActivityCard(
            Guid.NewGuid(), "GENSHIN", "EVENT · NEW", "测试活动", "日期待确认", "结束时间待确认",
            "https://example.invalid", "Change: NEW\nVerification: VerifiedOfficial\nGenshinOfficial · https://example.invalid 摘录", "VerifiedOfficial");

        Assert.Contains("变更： 新增", card.DisplayEvidenceSummary);
        Assert.Contains("核验： 官方已核验", card.DisplayEvidenceSummary);
        Assert.DoesNotContain("Change:", card.DisplayEvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Verification:", card.DisplayEvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VerifiedOfficial", card.DisplayEvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("原神官方公告", card.DisplayEvidenceSummary);
        Assert.DoesNotContain("GenshinOfficial", card.DisplayEvidenceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("原神", card.ToString());
        Assert.DoesNotContain("EvidenceSummary", card.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("VerifiedOfficial", card.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_DoesNotExposeGlobalBusinessRefreshButtons()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        Assert.DoesNotContain("Content=\"刷新游戏\" Command=\"{Binding RefreshGameDataCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"同步生日\" Command=\"{Binding RefreshBirthdayDataCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_KeepsSchedulerBehindAdvancedSettingsEntry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        Assert.DoesNotContain("Content=\"◇  任务调度\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"打开高级任务调度\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"任务调度\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_BindsEachBusinessRefreshActionToShellCommands()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        var pageRefreshButtons = new Dictionary<string, string>
        {
            ["采集今日美图"] = "RefreshArtworkDataCommand",
            ["刷新 GitHub 更新"] = "RefreshBgiDataCommand",
            ["重新读取状态"] = "RefreshSourceHealthCommand"
        };

        foreach (var (label, command) in pageRefreshButtons)
            Assert.Contains($"Content=\"{label}\" Command=\"{{Binding {command}}}\"", xaml, StringComparison.Ordinal);

        foreach (var command in pageRefreshButtons.Values)
            Assert.NotNull(typeof(ShellViewModel).GetProperty(command));
    }

    [Fact]
    public void MainWindow_BindsBothReportExportFormats()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));

        Assert.Contains("Content=\"导出 TXT\" Command=\"{Binding ExportTextReportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"导出 Markdown\" Command=\"{Binding ExportReportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.NotNull(typeof(ShellViewModel).GetProperty("ExportTextReportCommand"));
        Assert.NotNull(typeof(ShellViewModel).GetProperty("ExportReportCommand"));
        Assert.NotNull(typeof(ShellViewModel).GetProperty("OpenReportOutputDirectoryCommand"));
    }

    [Fact]
    public void MainWindow_BindsReportSectionEditActions()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        var actions = new Dictionary<string, string>
        {
            ["上移"] = "MoveReportSectionUpCommand",
            ["下移"] = "MoveReportSectionDownCommand",
            ["删除"] = "DeleteReportSectionCommand"
        };

        foreach (var (label, command) in actions)
        {
            Assert.Contains($"Content=\"{label}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"Command=\"{{Binding DataContext.{command},RelativeSource={{RelativeSource AncestorType=Window}}}}\"", xaml, StringComparison.Ordinal);
            Assert.NotNull(typeof(ShellViewModel).GetProperty(command));
        }
    }

    [Fact]
    public void MainWindow_BindsSingleReportSectionRebuildAndDeletedSectionRecovery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));

        Assert.Contains("Content=\"重新生成本段\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.RebuildReportSectionCommand,RelativeSource={RelativeSource AncestorType=Window}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding DeletedReportSections}\"", xaml, StringComparison.Ordinal);
        Assert.NotNull(typeof(ShellViewModel).GetProperty("RebuildReportSectionCommand"));
        Assert.NotNull(typeof(ShellViewModel).GetProperty("DeletedReportSections"));
    }

    [Fact]
    public void MainWindow_EditorCombosUseChineseDisplayTemplates()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var desktop = Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop");
        var xaml = File.ReadAllText(Path.Combine(desktop, "MainWindow.xaml"));
        var activityEditor = File.ReadAllText(Path.Combine(desktop, "ActivityEditorWindow.xaml"));
        var birthdayEditor = File.ReadAllText(Path.Combine(desktop, "BirthdayEditorWindow.xaml"));
        Assert.Contains("ItemsSource=\"{Binding ActivityTypeOptions}\"", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=type", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ActivityVerificationOptions}\"", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=status", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ActivityTimePrecisionOptions}\"", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=precision", activityEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding BirthdayFranchiseOptions}\" ItemTemplate=\"{StaticResource GameOptionTemplate}\"", birthdayEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox Text=\"{Binding BirthdayFranchise,UpdateSourceTrigger=PropertyChanged}\"", birthdayEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CalendarKindOptions}\" ItemTemplate=\"{StaticResource AutoOptionTemplate}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CalendarFranchiseOptions}\" ItemTemplate=\"{StaticResource AutoOptionTemplate}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_BrandsTheNavigationWithTheOfficialChineseName()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        Assert.Contains("Text=\"绮喵日报\" FontSize=\"28\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"QIMIAO DAILY\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_UserFacingMarkupDoesNotExposeInternalEnumLabels()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QimiaoDaily.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "QimiaoDaily.Desktop", "MainWindow.xaml"));
        var forbidden = new[] { "PENDING", "CONFIRMED", "ARCHIVED", "Run Now", "SUCCEEDED", "FAILED", "GENSHIN", "STARRAIL", "PREVIEW_NOTICE", "PREVIEW_LIVE", "VerifiedOfficial", "VerifiedMultiSource", "Unverified" };
        foreach (var value in forbidden)
        {
            Assert.DoesNotContain($"Text=\"{value}", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"Content=\"{value}", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"Header=\"{value}", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"ToolTip=\"{value}", xaml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task OpenEvidence_LoadsEvidenceAndRevisionHistoryFromSqlite()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-DesktopEvidence", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={Path.Combine(root, "vm.db")}").Options;
            await using var database = new QimiaoDailyDbContext(options);
            await database.Database.EnsureCreatedAsync();
            var item = new TimelineItem("GENSHIN", "EVENT", "Evidence item", VerificationStatus.VerifiedOfficial, "2026-08-20 10:00", "Asia/Shanghai", DateTimeOffset.UtcNow, TimePrecision.Exact, DateTimeOffset.UtcNow);
            item.AddEvidence(new EvidenceRecord("GenshinOfficial", "announcement", "https://example.invalid/evidence", "official excerpt", "test-parser", DateTimeOffset.UtcNow, "Page", DateTimeOffset.UtcNow, "UTC", DateTimeOffset.UtcNow, VerificationStatus.VerifiedOfficial));
            database.TimelineItems.Add(item);
            database.TimelineItemRevisions.Add(new TimelineItemRevision(Guid.NewGuid(), item.Id, "EndAt", "old", "new", "tester", "source update", DateTimeOffset.UtcNow));
            await database.SaveChangesAsync();

            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
            await viewModel.OpenEvidenceCommand.ExecuteAsync(item.Id);

            Assert.True(viewModel.IsEvidenceOpen);
            Assert.NotNull(viewModel.SelectedEvidence);
            Assert.Equal("https://example.invalid/evidence", viewModel.SelectedEvidence!.SourceUrl);
            Assert.Contains("[原神官方公告]", viewModel.SelectedEvidence.SourceText);
            Assert.DoesNotContain("[GenshinOfficial]", viewModel.SelectedEvidence.SourceText);
            Assert.Contains("source update", viewModel.SelectedEvidence.History);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void NavigationAndFilters_UpdateWorkspaceState()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-DesktopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using (var database = new QimiaoDailyDbContext(options))
            {
                database.Database.EnsureCreated();
                var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
                viewModel.ShowPage("游戏活动");

                Assert.Equal("游戏活动", viewModel.CurrentPage);
                Assert.True(viewModel.IsGameActivitiesPage);
                Assert.False(viewModel.IsOverviewPage);

                viewModel.GameFilter = "GENSHIN";
                viewModel.TypeFilter = "ENDGAME";
                Assert.Equal("GENSHIN", viewModel.GameFilter);
                Assert.Equal("ENDGAME", viewModel.TypeFilter);
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void BirthdayRefreshCommand_IsExposedForDirectWorkspaceSync()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-BirthdayCommand", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={Path.Combine(root, "vm.db")}").Options;
            using var database = new QimiaoDailyDbContext(options);
            database.Database.EnsureCreated();
            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");

            Assert.NotNull(viewModel.RefreshBirthdayDataCommand);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task BirthdayCandidates_ShowEvidenceAndUseServerReviewGate()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-BirthdayDesktop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            await using (var database = new QimiaoDailyDbContext(options))
            {
                await database.Database.EnsureCreatedAsync();
                database.Birthdays.AddRange(
                    new BirthdayEntity { Character = "Official", Franchise = "GENSHIN", Month = 9, Day = 28, Source = "HoYoWikiOfficial", SourceUrl = "https://example.invalid/official", Evidence = "Birthday: 9/28", VerificationStatus = VerificationStatus.VerifiedOfficial, VerifiedAt = DateTimeOffset.UtcNow },
                    new BirthdayEntity { Character = "Unknown", Franchise = "HI3", Month = 0, Day = 0, Source = "HI3 official list", SourceUrl = "https://example.invalid/unknown", Evidence = "Birthday field unavailable; UNKNOWN", VerificationStatus = VerificationStatus.Unverified, VerifiedAt = DateTimeOffset.UtcNow });
                await database.SaveChangesAsync();
                var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");

                Assert.Equal(2, viewModel.BirthdayCandidates.Count);
                Assert.Contains(viewModel.BirthdayCandidates, x => x.Character == "Official" && x.CanEnable && x.DateText == "09-28");
                Assert.Contains(viewModel.BirthdayCandidates, x => x.Character == "Unknown" && !x.CanEnable && x.DateText == "生日未知");
                var official = viewModel.BirthdayCandidates.Single(x => x.Character == "Official");
                await viewModel.ToggleBirthdayEnabledCommand.ExecuteAsync(official.Id);
            }

            await using var verify = new QimiaoDailyDbContext(options);
            Assert.True((await verify.Birthdays.SingleAsync(x => x.Character == "Official")).Enabled);
            Assert.False((await verify.Birthdays.SingleAsync(x => x.Character == "Unknown")).Enabled);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void Overview_LoadsComposedReportAndSplitsBgiRepositories()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-Overview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using var database = new QimiaoDailyDbContext(options);
            database.Database.EnsureCreated();
            var now = DateTimeOffset.UtcNow;
            var activity = new TimelineItem("GENSHIN", "EVENT", "首页真实确认活动", VerificationStatus.VerifiedOfficial, "today", "Asia/Shanghai", now, TimePrecision.Exact, now);
            activity.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/home", "首页证据", "test", now, verificationStatus: VerificationStatus.VerifiedOfficial));
            activity.Confirm("test", "home", now);
            database.TimelineItems.Add(activity);
            database.GitCommitRecords.AddRange(
                new GitCommitRecord { Repository = "babalae/better-genshin-impact", Sha = "abcdef1234567", Subject = "本体提交", Url = "https://github.com/babalae/better-genshin-impact/commit/abcdef1234567", CommitterDate = now, FetchedAt = now },
                new GitCommitRecord { Repository = "babalae/bettergi-scripts-list", Sha = "1234567abcdef", Subject = "脚本提交", Url = "https://github.com/babalae/bettergi-scripts-list/commit/1234567abcdef", CommitterDate = now, FetchedAt = now });
            database.SaveChanges();

            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");

            Assert.Contains("首页真实确认活动", viewModel.ComposedReport);
            Assert.Single(viewModel.BgiMainCommits);
            Assert.Single(viewModel.BgiScriptsCommits);
            Assert.Contains("Asia/Shanghai", viewModel.BgiWindowText);
            Assert.Contains(viewModel.BgiWindowStatus, new[] { "WINDOW COMPLETE", "COMMIT WINDOW INCOMPLETE" });
            Assert.Equal("abcdef1", viewModel.BgiMainCommits[0].Sha);
            Assert.Equal("1234567", viewModel.BgiScriptsCommits[0].Sha);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void Calendar_ExposesFullYearAndSupportsTextKindAndFranchiseFilters()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-CalendarFilters", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using var database = new QimiaoDailyDbContext(options);
            database.Database.EnsureCreated();
            database.Birthdays.Add(new BirthdayEntity { Character = "Filter Character", Franchise = "GENSHIN", Month = 1, Day = 2, Source = "official", SourceUrl = "https://example.invalid/filter", Evidence = "Birthday: 1/2", VerificationStatus = VerificationStatus.VerifiedOfficial, VerifiedAt = DateTimeOffset.UtcNow, Enabled = true });
            database.SaveChanges();

            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
            Assert.Equal(DateTime.IsLeapYear(DateTime.Now.Year) ? 366 : 365, viewModel.YearCalendarDays.Count);
            var birthdayDay = viewModel.YearCalendarDays.Single(x => x.Date.Month == 1 && x.Date.Day == 2);
            Assert.Contains("原神", birthdayDay.Details);
            Assert.DoesNotContain("GENSHIN", birthdayDay.Details, StringComparison.Ordinal);

            viewModel.CalendarSearchText = "Filter Character";
            Assert.Single(viewModel.YearCalendarDays);
            viewModel.CalendarSearchText = string.Empty;
            viewModel.CalendarKindFilter = "BIRTHDAY";
            Assert.Single(viewModel.YearCalendarDays);
            viewModel.CalendarKindFilter = "全部";
            viewModel.CalendarFranchiseFilter = "GENSHIN";
            Assert.Single(viewModel.YearCalendarDays);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void ArtworkPage_ExposesLatestDailyRunCountAndFailureReason()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-ArtworkRun", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using var database = new QimiaoDailyDbContext(options);
            database.Database.EnsureCreated();
            database.ArtworkDailyRuns.Add(new ArtworkDailyRunEntity
            {
                TargetCount = 30,
                FetchedCount = 24,
                NewCandidateCount = 24,
                Status = "PARTIAL",
                FailureReason = "仅找到 24 个真正新候选。",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletedAt = DateTimeOffset.UtcNow
            });
            database.SaveChanges();

            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");

            Assert.Equal("24/30", viewModel.ArtworkRunCountText);
            Assert.Equal("PARTIAL", viewModel.ArtworkRunStatus);
            Assert.Equal("部分成功", viewModel.ArtworkRunStatusDisplay);
            Assert.Contains("24", viewModel.ArtworkRunMessage);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }


    [Fact]
    public async Task CalendarEditorCommands_RequestModalEditors()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-CalendarEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={Path.Combine(root, "vm.db")}").Options;
            await using var database = new QimiaoDailyDbContext(options);
            await database.Database.EnsureCreatedAsync();
            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
            var birthdayRequested = 0;
            var anniversaryRequested = 0;
            viewModel.BirthdayEditorRequested += () => birthdayRequested++;
            viewModel.AnniversaryEditorRequested += () => anniversaryRequested++;

            viewModel.BeginBirthdayCreateCommand.Execute(null);
            viewModel.BeginAnniversaryCreateCommand.Execute(null);

            Assert.Equal(1, birthdayRequested);
            Assert.Equal(1, anniversaryRequested);
            Assert.True(DateOnly.TryParseExact(viewModel.AnniversaryStartedOn, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task BirthdayEditor_SavesManualBirthdayEnabledAndLoadsItForEditing()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-BirthdayEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            await using var database = new QimiaoDailyDbContext(options);
            await database.Database.EnsureCreatedAsync();
            var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
            viewModel.ShowPage("生日审核");
            Assert.True(viewModel.IsBirthdayEditorPage);
            viewModel.BirthdayCharacter = "Editor Character";
            viewModel.BirthdayFranchise = "HI3";
            viewModel.BirthdayMonthText = "7";
            viewModel.BirthdayDayText = "8";
            viewModel.BirthdaySource = "Manual official evidence";
            viewModel.BirthdaySourceUrl = "https://example.invalid/hi3-birthday";
            viewModel.BirthdayEvidence = "Evidence excerpt";

            await viewModel.SaveBirthdayCommand.ExecuteAsync(null);

            var item = await database.Birthdays.SingleAsync();
            Assert.Equal("Editor Character", item.Character);
            Assert.True(item.Enabled);
            Assert.Equal(VerificationStatus.Unverified, item.VerificationStatus);

            viewModel.EditBirthdayCommand.Execute(item.Id);
            Assert.Equal(item.Id, viewModel.EditingBirthdayId);
            Assert.Equal("Editor Character", viewModel.BirthdayCharacter);
            Assert.Equal("7", viewModel.BirthdayMonthText);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task ActivityEditor_SavesAuditRevisionAndReturnsCandidateToPending()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-ActivityEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            await using (var database = new QimiaoDailyDbContext(options))
            {
                await database.Database.EnsureCreatedAsync();
                var now = DateTimeOffset.UtcNow;
                var item = new TimelineItem("GENSHIN", "EVENT", "待编辑活动", VerificationStatus.VerifiedOfficial,
                    "2026-08-18", "Asia/Shanghai", now, TimePrecision.Exact, now, now.AddDays(1));
                item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/activity", "activity evidence", "test", now));
                item.Confirm("tester", "initial confirmation", now);
                database.TimelineItems.Add(item);
                await database.SaveChangesAsync();

                var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");
                var editorRequested = false;
                viewModel.ActivityEditorRequested += () => editorRequested = true;
                await viewModel.OpenEvidenceCommand.ExecuteAsync(item.Id);
                viewModel.EditActivityCommand.Execute(item.Id);

                Assert.True(editorRequested);
                Assert.Equal(item.Id, viewModel.EditingActivityId);
                viewModel.ActivityEditType = "VIDEO";
                viewModel.ActivityEditTitle = "编辑后的活动";
                viewModel.ActivityEditNormalizedText = "2026-08-18 12:00";
                viewModel.ActivityEditEndText = "2026-08-19 12:00";
                viewModel.ActivityEditReason = "修正官方分类";
                await viewModel.SaveActivityEditCommand.ExecuteAsync(null);

                Assert.StartsWith("游戏候选已保存", viewModel.ImportMessage, StringComparison.Ordinal);
            }

            await using var verify = new QimiaoDailyDbContext(options);
            var saved = await verify.TimelineItems.SingleAsync();
            Assert.Equal("编辑后的活动", saved.Title);
            Assert.Equal("VIDEO", saved.ItemType);
            Assert.Equal(ReviewStatus.Pending, saved.ReviewStatus);
            Assert.Equal(TimeSpan.FromHours(8), saved.NormalizedTime!.Value.Offset);
            Assert.Contains(await verify.ReviewActions.ToListAsync(), x => x.Action == "EDIT" && x.Reason == "修正官方分类");
            Assert.Contains(await verify.TimelineItemRevisions.ToListAsync(), x => x.FieldName == "Title" && x.NewValue == "编辑后的活动");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task ArtworkReview_MarksAndConfirmsThroughShortcutAndEditsMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-ArtworkReview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            await using (var database = new QimiaoDailyDbContext(options))
            {
                await database.Database.EnsureCreatedAsync();
                database.Artworks.Add(new ArtworkEntity
                {
                    Platform = "PIXIV", ArtworkId = "desktop-art-1", NormalizedUrl = "https://www.pixiv.net/artworks/desktop-art-1",
                    Title = "Artwork before", Author = "artist", SourceUrl = "https://www.pixiv.net/artworks/desktop-art-1",
                    ThumbnailUrl = "https://i.pximg.net/desktop-art-1.jpg", PublishedAt = DateTimeOffset.UtcNow, FetchedAt = DateTimeOffset.UtcNow,
                    ReviewStatus = ReviewStatus.Pending
                });
                await database.SaveChangesAsync();

                var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test")
                {
                    ArtworkBatchReason = "desktop artwork review"
                };
                viewModel.ShowPage("美图分享");
                var item = viewModel.PendingArtworks.Single();
                viewModel.ToggleArtworkMarkedCommand.Execute(item.Id);
                Assert.Equal(1, viewModel.SelectedArtworkCount);
                await viewModel.HandleArtworkShortcutAsync("C");

                var confirmed = viewModel.ConfirmedArtworks.Single();
                var editorRequested = false;
                viewModel.ArtworkEditorRequested += () => editorRequested = true;
                viewModel.EditArtworkCommand.Execute(confirmed.Id);
                Assert.True(editorRequested);
                viewModel.ArtworkEditTitle = "Artwork after";
                viewModel.ArtworkEditCharacterName = "Character";
                viewModel.ArtworkEditFranchiseName = "GENSHIN";
                viewModel.ArtworkEditCategory = "ILLUST";
                viewModel.ArtworkEditTags = "tag";
                viewModel.ArtworkBatchReason = "desktop metadata correction";
                await viewModel.SaveArtworkEditCommand.ExecuteAsync(null);
                Assert.StartsWith("美图已保存", viewModel.ImportMessage, StringComparison.Ordinal);
            }

            await using var verify = new QimiaoDailyDbContext(options);
            var saved = await verify.Artworks.SingleAsync();
            Assert.Equal(ReviewStatus.Confirmed, saved.ReviewStatus);
            Assert.Equal(("Artwork after", "Character", "GENSHIN", "ILLUST", "tag"), (saved.Title, saved.CharacterName, saved.FranchiseName, saved.Category, saved.Tags));
            Assert.Contains(await verify.ArtworkReviewActions.ToListAsync(), x => x.Action == "CONFIRM");
            Assert.Contains(await verify.ArtworkReviewActions.ToListAsync(), x => x.Action == "EDIT" && x.Reason == "desktop metadata correction");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void GameCards_DisplayPersistedChangeClassificationBesideType()
    {
        var root = Path.Combine(Path.GetTempPath(), "QimiaoDaily-ChangeCard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "vm.db");
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using (var database = new QimiaoDailyDbContext(options))
            {
                database.Database.EnsureCreated();
                var item = new TimelineItem("GENSHIN", "EVENT", "Changed activity", VerificationStatus.VerifiedOfficial, "2026-08-20", "Asia/Shanghai", DateTimeOffset.UtcNow, TimePrecision.Exact, DateTimeOffset.UtcNow);
                item.SetCanonicalIdentity("GENSHIN:changed-1");
                item.SetChangeKind(TimelineChangeKind.TimeChanged);
                item.AddEvidence(new EvidenceRecord("official", "notice", "https://example.invalid/changed", "changed evidence", "test", DateTimeOffset.UtcNow));
                database.TimelineItems.Add(item);
                database.SaveChanges();
                var viewModel = new ShellViewModel(database, 0, 0, 0, 0, 0, "test");

                Assert.Contains("TIME_CHANGED", viewModel.PendingActivities.Single().Type);
                Assert.Contains("TIME_CHANGED", viewModel.PendingActivities.Single().EvidenceSummary);
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
