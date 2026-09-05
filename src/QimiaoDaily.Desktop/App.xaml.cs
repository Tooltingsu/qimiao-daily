using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Collectors;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.Desktop;

public partial class App : Application
{
    private SchedulerBackgroundService? _scheduler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var paths = new QimiaoDailyPaths();
        paths.EnsureDirectories();
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite($"Data Source={paths.DatabasePath}")
            .Options;

        using var database = new QimiaoDailyDbContext(options);
        QimiaoDatabaseInitializer.EnsureReady(database);
        new ManualDataMigrationService(database)
            .PromoteConfirmedGameCalendarEntriesAsync()
            .GetAwaiter().GetResult();
        var shanghai = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time");
        new EndgameScheduleMaintenanceService(database)
            .RefreshAllRulesAsync(DateOnly.FromDateTime(shanghai.Date))
            .GetAwaiter().GetResult();
        new OperationsService(database).EnsureDefaultsAsync().GetAwaiter().GetResult();
        new TimelineArchiveService(database).ArchiveExpiredAsync(DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _scheduler = new SchedulerBackgroundService(
            () => new QimiaoDailyDbContext(options),
            ExecuteSchedulerTaskAsync,
            SchedulerScheduleCatalog.Load(paths.ConfigDirectory));
        if (Environment.GetEnvironmentVariable("QIMIAO_UI_DEMO") == "1")
        {
            SeedUiDemo(database, paths);
            SeedArchiveAndConflictDemo(database);
        }

        var window = new MainWindow
        {
            DataContext = QimiaoDaily.Desktop.MainWindow.CreateViewModel(database, "本地数据库已就绪。")
        };

        if (Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_PAGE") is string page)
        {
            window.Loaded += (_, _) => SchedulePageCapture(window, page, paths);
        }
        else if (Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_CALENDAR") == "1")
        {
            window.Loaded += (_, _) => ScheduleCalendarCapture(window, paths);
        }

        window.Show();
        _ = Task.Run(() => CacheSelectedArtworkThumbnails(paths))
            .ContinueWith(_ => window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (window.DataContext is ViewModels.ShellViewModel viewModel) viewModel.RefreshArtworkCards();
            })), TaskScheduler.Default);
    }

    private static async Task<int> ExecuteSchedulerTaskAsync(QimiaoDailyDbContext database, string taskKey, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        return await new SchedulerTaskExecutor(database, client, new QimiaoDailyPaths()).ExecuteAsync(taskKey, cancellationToken);
    }

    private static void CacheSelectedArtworkThumbnails(QimiaoDailyPaths paths)
    {
        try
        {
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={paths.DatabasePath}").Options;
            using var database = new QimiaoDailyDbContext(options);
            var selected = database.Artworks
                .Where(x => x.ReviewStatus == ReviewStatus.Confirmed && x.SelectedForReport)
                .ToList()
                .Where(x => x.ThumbnailUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (selected.Count == 0) return;
            var cookie = OperatingSystem.IsWindows() ? new SecureSettingsStore(paths).TryGet("pixiv_session") : null;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var cache = new ArtworkThumbnailCacheService(client, paths, cookie);
            foreach (var artwork in selected) cache.TryCacheAsync(artwork).GetAwaiter().GetResult();
            database.SaveChanges();
        }
        catch
        {
            // A thumbnail outage must not prevent the desktop workbench from opening.
        }
    }

    private static void SchedulePageCapture(MainWindow window, string page, QimiaoDailyPaths paths)
    {
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
        {
            try
            {
                var viewModel = (ViewModels.ShellViewModel)window.DataContext;
                var selectedPage = page switch
                {
                    "overview" => "概览",
                    "game" => "游戏活动",
                    "artwork" => "美图分享",
                    "bgi" => "BGI",
                    "report" => "日报编辑器",
                    "source-health" => "来源健康",
                    "scheduler" => "任务调度",
                    "settings" => "设置",
                    _ => page
                };
                viewModel.ShowPage(selectedPage);
                var gameTab = Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_GAME_TAB");
                if (string.Equals(gameTab, "endgame", StringComparison.OrdinalIgnoreCase))
                    window.SelectGameActivityTab("周期玩法");
                if (Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_EVIDENCE") == "1")
                {
                    var requestedTitle = Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_EVIDENCE_TITLE");
                    var id = viewModel.PendingActivities.FirstOrDefault(x => requestedTitle is not null && x.Title.Contains(requestedTitle, StringComparison.OrdinalIgnoreCase))?.Id
                        ?? viewModel.PendingActivities.FirstOrDefault()?.Id
                        ?? viewModel.ConfirmedActivities.FirstOrDefault()?.Id;
                    if (id is { } evidenceId)
                    {
                        try { await viewModel.OpenEvidenceCommand.ExecuteAsync(evidenceId); }
                        catch { }
                    }
                }
                var target = Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_PATH") ?? Path.Combine(paths.Root, "capture.png");
                await Task.Delay(3000);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    window.UpdateLayout();
                    CaptureWindow(window, target);
                }, DispatcherPriority.ApplicationIdle);
            }
            catch { }
        }));
    }

    private static void ScheduleCalendarCapture(MainWindow window, QimiaoDailyPaths paths)
    {
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
        {
            var viewModel = (ViewModels.ShellViewModel)window.DataContext;
            viewModel.ShowPage("日历");
            if (DateTime.TryParse(Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_DATE"), out var date))
            {
                viewModel.ShowCalendarDate(date);
            }
            if (Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_BIRTHDAY") == "1")
                window.ScrollCalendarDetailsToEnd();

            var target = Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_PATH") ?? Path.Combine(paths.Root, "calendar.png");
            await Task.Delay(3000);
            await window.Dispatcher.InvokeAsync(() =>
            {
                window.UpdateLayout();
                CaptureWindow(window, target);
            }, DispatcherPriority.ApplicationIdle);
        }));
    }

    private static void CaptureWindow(Window window, string path)
    {
        if (window is MainWindow mainWindow)
        {
            mainWindow.NormalizeWorkspaceLabels();
        }
        var width = Math.Max(1, (int)window.ActualWidth);
        var height = Math.Max(1, (int)window.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void SeedUiDemo(QimiaoDailyDbContext database, QimiaoDailyPaths paths)
    {
        var now = DateTimeOffset.UtcNow;
        if (!database.TimelineItems.Any())
        {
            var item = new TimelineItem("GENSHIN", "EVENT", "UI review demo candidate", VerificationStatus.VerifiedOfficial,
                "2026-08-14 10:00", "Asia/Shanghai", now, TimePrecision.Exact, now, now.AddDays(4));
            item.AddEvidence(new EvidenceRecord("OfficialDemo", "announcement", "https://example.invalid/official-demo",
                "Isolated UI verification evidence.", "ui-demo", now));
            database.TimelineItems.Add(item);
        }

        if (!database.Artworks.Any())
        {
            database.Artworks.Add(new ArtworkEntity
            {
                Platform = "PIXIV",
                ArtworkId = "100000000",
                NormalizedUrl = "https://www.pixiv.net/artworks/100000000",
                Title = "Pixiv UI review demo",
                Author = "銇°伄銇欍亼",
                AuthorId = "57190277",
                SourceUrl = "https://www.pixiv.net/artworks/100000000",
                ThumbnailUrl = DownloadDemoThumbnail(paths),
                PublishedAt = now,
                FetchedAt = now,
                ReviewStatus = ReviewStatus.Pending
            });
        }

        if (!database.ArtworkDailyRuns.Any())
        {
            database.ArtworkDailyRuns.Add(new ArtworkDailyRunEntity
            {
                Provider = "Pixiv",
                TargetCount = 30,
                FetchedCount = 24,
                NewCandidateCount = 24,
                Status = "PARTIAL",
                FailureReason = "UI 演示：本次仅找到 24 个真正新候选，未使用旧图补足。",
                StartedAt = now.AddMinutes(-2),
                CompletedAt = now
            });
        }

        if (!database.Birthdays.Any())
        {
            database.Birthdays.Add(new BirthdayEntity
            {
                Character = "Calendar Demo",
                Franchise = "GENSHIN",
                Month = 8,
                Day = 14,
                Source = "OfficialDemo",
                SourceUrl = "https://example.invalid/calendar-demo",
                Evidence = "Isolated calendar verification evidence.",
                VerificationStatus = VerificationStatus.VerifiedOfficial,
                VerifiedAt = now,
                Enabled = true
            });
        }

        if (!database.Anniversaries.Any())
        {
            database.Anniversaries.Add(new AnniversaryEntity
            {
                Title = "Demo Anniversary",
                StartedOn = new DateOnly(2020, 8, 14),
                Enabled = true
            });
        }

        if (!database.GitCommitRecords.Any())
        {
            var repositories = SourceSettings.Load(paths).BgiRepositories;
            var primaryRepository = repositories.First();
            var secondaryRepository = repositories.Skip(1).FirstOrDefault() ?? primaryRepository;
            database.GitCommitRecords.Add(new GitCommitRecord
            {
                Repository = primaryRepository,
                Sha = "demo-bgi-001",
                Subject = "Demo BGI update",
                Url = "https://github.com/" + primaryRepository + "/commit/demo-bgi-001",
                FetchedAt = now,
                SelectedForReport = true
            });
            database.GitCommitRecords.Add(new GitCommitRecord
            {
                Repository = secondaryRepository,
                Sha = "demo-script-001",
                Subject = "Demo script update",
                Url = "https://github.com/" + secondaryRepository + "/commit/demo-script-001",
                FetchedAt = now,
                SelectedForReport = false
            });
        }

        if (!database.ProviderHealthRecords.Any())
        {
            database.ProviderHealthRecords.AddRange(
                new ProviderHealthRecord { ProviderName = "GenshinOfficial", Status = "HEALTHY", LastSuccessAt = now, LastLatencyMs = 284, ItemCount = 12, ParserStatus = "OK" },
                new ProviderHealthRecord { ProviderName = "Pixiv", Status = "LOGIN_REQUIRED", LastFailureAt = now, LastLatencyMs = 913, ParserStatus = "READY", FailureCount = 1, LastError = "Session is not configured." });
        }
        if (!database.SchedulerTaskRecords.Any())
        {
            database.SchedulerTaskRecords.AddRange(
                new SchedulerTaskRecord { TaskKey = "game_data_refresh", DisplayName = "游戏数据刷新", ScheduleText = "每 30 分钟", Status = "IDLE", MaxRetries = 3 },
                new SchedulerTaskRecord { TaskKey = "github_bgi_refresh", DisplayName = "BGI GitHub 更新", ScheduleText = "每日 18:05", Status = "SUCCEEDED", LastRunAt = now, MaxRetries = 3 },
                new SchedulerTaskRecord { TaskKey = "artwork_daily_search", DisplayName = "美图每日采集", ScheduleText = "每日 09:00", Status = "WARNING", FailureCount = 1, LastError = "Pixiv session required.", MaxRetries = 2 });
        }

        foreach (var provider in new[] { "StarRailOfficial", "NteBilibiliOfficial", "BGI GitHub" })
        {
            if (!database.ProviderHealthRecords.Any(x => x.ProviderName == provider))
                database.ProviderHealthRecords.Add(new ProviderHealthRecord { ProviderName = provider, Status = provider == "NteBilibiliOfficial" ? "BLOCKED" : "HEALTHY", LastSuccessAt = provider == "NteBilibiliOfficial" ? null : now, LastFailureAt = provider == "NteBilibiliOfficial" ? now : null, ParserStatus = "READY", LastError = provider == "NteBilibiliOfficial" ? "Bilibili API access is blocked." : null });
        }
        var taskSeeds = new[]
        {
            ("video_refresh", "视频刷新", "每 60 分钟"), ("preview_refresh", "前瞻刷新", "每 60 分钟"),
            ("endgame_refresh", "深渊周期刷新", "每日 04:00"), ("github_scripts_refresh", "脚本仓库更新", "每日 18:05"),
            ("birthday_character_refresh", "生日数据刷新", "每日 06:00"), ("calendar_refresh", "日历刷新", "每日 00:10"),
            ("nte_official_refresh", "异环官网更新", "每 30 分钟"), ("nte_bilibili_refresh", "异环 Bilibili 更新", "每 60 分钟"),
            ("archive_cleanup", "归档清理", "每日 03:59"), ("report_build", "日报生成", "每日 08:00")
        };
        foreach (var seed in taskSeeds)
        {
            if (!database.SchedulerTaskRecords.Any(x => x.TaskKey == seed.Item1)) database.SchedulerTaskRecords.Add(new SchedulerTaskRecord { TaskKey = seed.Item1, DisplayName = seed.Item2, ScheduleText = seed.Item3, Status = "IDLE", MaxRetries = 3 });
        }
        database.SaveChanges();
    }

    private static string DownloadDemoThumbnail(QimiaoDailyPaths paths)
    {
        const string url = "https://i.pximg.net/c/250x250_80_a2/custom-thumb/img/2022/07/26/02/35/50/100000000_p0_custom1200.jpg";
        if (Environment.GetEnvironmentVariable("QIMIAO_CAPTURE_PAGE") is not null)
        {
            return url;
        }

        var local = Path.Combine(paths.ImagesDirectory, "pixiv-100000000-thumb.jpg");
        if (File.Exists(local))
        {
            return local;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.pixiv.net/");
            File.WriteAllBytes(local, client.GetByteArrayAsync(url).GetAwaiter().GetResult());
            return local;
        }
        catch
        {
            return url;
        }
    }

    private static void SeedArchiveAndConflictDemo(QimiaoDailyDbContext database)
    {
        var now = DateTimeOffset.UtcNow;
        if (!database.TimelineItems.Any(x => x.Title == "Archive UI demo"))
        {
            var archived = new TimelineItem("NTE", "EVENT", "Archive UI demo", VerificationStatus.VerifiedOfficial, "2026-08-14", "Asia/Shanghai", now, TimePrecision.DateOnly, now, now.AddDays(-5));
            archived.AddEvidence(new EvidenceRecord("NteOfficialWebsite", "official-news", "https://example.invalid/archive-demo", "Isolated archive verification evidence.", "ui-demo", now));
            archived.Archive("ui-demo", "Archive page fixture");
            database.TimelineItems.Add(archived);
        }
        if (!database.TimelineItems.Any(x => x.Title == "Conflict UI demo"))
        {
            var conflict = new TimelineItem("GENSHIN", "EVENT", "Conflict UI demo", VerificationStatus.Conflict, "2026-08-15 10:00", "Asia/Shanghai", now.AddHours(-1), TimePrecision.Exact, now, now.AddDays(3));
            conflict.SetCanonicalIdentity("GENSHIN:ui-conflict-demo");
            conflict.SetChangeKind(TimelineChangeKind.Conflict);
            conflict.AddEvidence(new EvidenceRecord("OfficialSourceA", "announcement", "https://example.invalid/conflict-a", "Source A says the event starts at 10:00.", "ui-demo", now));
            conflict.AddEvidence(new EvidenceRecord("OfficialSourceB", "announcement", "https://example.invalid/conflict-b", "Source B says the event starts at 12:00.", "ui-demo", now));
            database.TimelineItems.Add(conflict);
        }
        database.SaveChanges();
    }
}
