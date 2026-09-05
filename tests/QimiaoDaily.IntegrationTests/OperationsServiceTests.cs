using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class OperationsServiceTests
{
    [Fact]
    public void SchedulerScheduleCatalog_LoadsOverridesAndComputesChinaDailyBoundary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qimiao-schedule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "scheduler.json"), "{\"video_refresh\":{\"mode\":\"interval\",\"intervalMinutes\":5},\"github_bgi_refresh\":{\"mode\":\"daily\",\"localTime\":\"18:05\"},\"bad\":{\"mode\":\"interval\",\"intervalMinutes\":0}}");
            var catalog = SchedulerScheduleCatalog.Load(directory);
            var now = new DateTimeOffset(2026, 8, 15, 9, 0, 0, TimeSpan.Zero);
            Assert.Equal(now.AddMinutes(5), catalog.NextRun("video_refresh", now));
            Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 5, 0, TimeSpan.Zero), catalog.NextRun("github_bgi_refresh", now));
            var afterDailyRun = new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.Zero);
            Assert.Equal(new DateTimeOffset(2026, 8, 16, 10, 5, 0, TimeSpan.Zero), catalog.NextRun("github_bgi_refresh", afterDailyRun));
            Assert.Equal(60, catalog.Get("bad").IntervalMinutes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SchedulerScheduleCatalog_MalformedConfigFallsBackToDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qimiao-schedule-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "scheduler.json"), "not-json");
            var catalog = SchedulerScheduleCatalog.Load(directory);
            Assert.Equal(60, catalog.Get("video_refresh").IntervalMinutes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureDefaults_RegistersOnlyV3AutomaticTasks()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options); await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        await new OperationsService(db).EnsureDefaultsAsync();
        Assert.Equal(16, await db.ProviderHealthRecords.CountAsync());
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "NteOfficialRoster"));
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "NteGameBirthday"));
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "NteFandomBirthday"));
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "Hi3BiligameBirthday"));
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "Hi3BaiduBirthday"));
        Assert.True(await db.ProviderHealthRecords.AnyAsync(x => x.ProviderName == "Hi3MoegirlBirthday"));
        var taskKeys = await db.SchedulerTaskRecords
            .Select(x => x.TaskKey)
            .OrderBy(x => x)
            .ToListAsync();
        Assert.Equal(
            ["archive_cleanup", "artwork_daily_search", "calendar_refresh", "github_bgi_refresh", "github_scripts_refresh", "nte_bilibili_refresh", "preview_refresh", "report_build", "video_refresh"],
            taskKeys);
    }

    [Theory]
    [InlineData("game_data_refresh")]
    [InlineData("birthday_character_refresh")]
    [InlineData("endgame_refresh")]
    [InlineData("nte_official_refresh")]
    public async Task EnsureDefaults_DisablesExistingRetiredAutomaticTask(string taskKey)
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.SchedulerTaskRecords.Add(new SchedulerTaskRecord
        {
            TaskKey = taskKey,
            DisplayName = "Retired",
            Status = "IDLE",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        await new OperationsService(db).EnsureDefaultsAsync();

        var task = await db.SchedulerTaskRecords.SingleAsync(x => x.TaskKey == taskKey);
        Assert.Equal("DISABLED", task.Status);
        Assert.Null(task.NextRunAt);
        Assert.Contains("V3", task.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderFailure_IsPersistedAsVisibleHealthState()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var service = new OperationsService(db);

        await service.RecordFailureAsync("Pixiv", "LOGIN_REQUIRED", "Session is not configured.", 913);

        var item = Assert.Single(await service.GetHealthAsync());
        Assert.Equal("LOGIN_REQUIRED", item.Status);
        Assert.Equal(1, item.FailureCount);
        Assert.Contains("Session", item.LastError);
    }

    [Fact]
    public async Task RunNow_RetriesAtMostConfiguredLimitAndMarksFailed()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.SchedulerTaskRecords.Add(new SchedulerTaskRecord { TaskKey = "demo", DisplayName = "Demo", MaxRetries = 2 });
        await db.SaveChangesAsync();
        var service = new OperationsService(db);
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunNowAsync("demo", _ =>
        {
            attempts++;
            throw new InvalidOperationException("network failed");
        }));

        var item = Assert.Single(await service.GetTasksAsync());
        Assert.Equal(3, attempts);
        Assert.Equal("FAILED", item.Status);
        Assert.Equal(3, item.FailureCount);
    }

    [Theory]
    [InlineData("game_data_refresh")]
    [InlineData("birthday_character_refresh")]
    [InlineData("endgame_refresh")]
    [InlineData("nte_official_refresh")]
    public async Task RunNow_RejectsRetiredAutomaticTaskWithoutInvokingAction(string taskKey)
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.SchedulerTaskRecords.Add(new SchedulerTaskRecord { TaskKey = taskKey, DisplayName = "Retired" });
        await db.SaveChangesAsync();
        var calls = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OperationsService(db).RunNowAsync(taskKey, _ =>
            {
                calls++;
                return Task.FromResult(1);
            }));

        var task = await db.SchedulerTaskRecords.SingleAsync();
        Assert.Equal(0, calls);
        Assert.Equal("DISABLED", task.Status);
        Assert.Contains("V3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackgroundScheduler_RunsDueV3HandlerAndSchedulesNextRun()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "qimiao-scheduler-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.SchedulerTaskRecords.Add(new SchedulerTaskRecord
        {
            TaskKey = "video_refresh", DisplayName = "Video", MaxRetries = 0,
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var calls = 0;
        await using var scheduler = new SchedulerBackgroundService(
            () => new QimiaoDailyDbContext(options),
            (_, _, _) => { calls++; return Task.FromResult(4); });

        await scheduler.RunOnceAsync();

        var task = await db.SchedulerTaskRecords.SingleAsync();
        await db.Entry(task).ReloadAsync();
        Assert.Equal(1, calls);
        Assert.Equal("SUCCEEDED", task.Status);
        Assert.True(task.NextRunAt > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("game_data_refresh")]
    [InlineData("birthday_character_refresh")]
    [InlineData("endgame_refresh")]
    [InlineData("nte_official_refresh")]
    public async Task BackgroundScheduler_DoesNotRunRetiredAutomaticTask(string taskKey)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "qimiao-retired-scheduler-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var dueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        db.SchedulerTaskRecords.Add(new SchedulerTaskRecord
        {
            TaskKey = taskKey, DisplayName = "Retired", MaxRetries = 0, NextRunAt = dueAt
        });
        await db.SaveChangesAsync();
        var calls = 0;
        await using var scheduler = new SchedulerBackgroundService(
            () => new QimiaoDailyDbContext(options),
            (_, _, _) => { calls++; return Task.FromResult(4); });

        await scheduler.RunOnceAsync();

        var task = await db.SchedulerTaskRecords.SingleAsync();
        await db.Entry(task).ReloadAsync();
        Assert.Equal(0, calls);
        Assert.Equal("DISABLED", task.Status);
        Assert.Null(task.NextRunAt);
        Assert.Contains("V3", task.LastError, StringComparison.Ordinal);
    }
}
