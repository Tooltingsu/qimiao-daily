using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed record ProviderHealthSnapshot(string ProviderName, string Status, DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFailureAt, long LastLatencyMs, int ItemCount, string ParserStatus, int FailureCount, string? LastError);
public sealed record SchedulerTaskSnapshot(string TaskKey, string DisplayName, string ScheduleText, string Status, DateTimeOffset? LastRunAt, DateTimeOffset? NextRunAt, int FailureCount, int MaxRetries, string? LastError);

public sealed class OperationsService(QimiaoDailyDbContext database)
{
    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var providers = new[] { "GenshinOfficial", "StarRailOfficial", "NteBilibiliOfficial", "NteOfficialWebsite", "NteOfficialRoster", "NteGameBirthday", "OfficialYoutubeRSS:Genshin", "OfficialYoutubeRSS:StarRail", "BirthdayHoYoWiki", "Honkai3OfficialCharacterList", "Hi3BiligameBirthday", "Hi3BaiduBirthday", "Hi3MoegirlBirthday", "NteFandomBirthday", "Pixiv", "BGI GitHub" };
        foreach (var provider in providers)
        {
            if (!await database.ProviderHealthRecords.AnyAsync(x => x.ProviderName == provider, cancellationToken)) database.ProviderHealthRecords.Add(new ProviderHealthRecord { ProviderName = provider, Status = "WARNING", ParserStatus = "NOT_RUN" });
        }
        var tasks = new[]
        {
            ("video_refresh", "视频刷新", "每 60 分钟"), ("preview_refresh", "前瞻刷新", "每 60 分钟"),
            ("github_bgi_refresh", "BGI GitHub 更新", "每日 18:05"), ("github_scripts_refresh", "脚本仓库更新", "每日 18:05"),
            ("nte_bilibili_refresh", "异环 Bilibili 更新", "每 60 分钟"),
            ("artwork_daily_search", "美图每日采集", "每日 09:00"), ("calendar_refresh", "日历刷新", "每日 00:10"),
            ("archive_cleanup", "归档清理", "每日 03:59"), ("report_build", "日报生成", "每日 08:00")
        };
        foreach (var task in tasks)
        {
            if (!await database.SchedulerTaskRecords.AnyAsync(x => x.TaskKey == task.Item1, cancellationToken)) database.SchedulerTaskRecords.Add(new SchedulerTaskRecord { TaskKey = task.Item1, DisplayName = task.Item2, ScheduleText = task.Item3, Status = "IDLE", MaxRetries = 3 });
        }
        var retiredTasks = await database.SchedulerTaskRecords
            .Where(x => x.TaskKey == "game_data_refresh" || x.TaskKey == "birthday_character_refresh" || x.TaskKey == "endgame_refresh" || x.TaskKey == "nte_official_refresh")
            .ToListAsync(cancellationToken);
        foreach (var retiredTask in retiredTasks)
            DisableRetiredAutomaticTask(retiredTask);
        await database.SaveChangesAsync(cancellationToken);
    }

    public static void DisableRetiredAutomaticTask(SchedulerTaskRecord task)
    {
        task.Status = "DISABLED";
        task.NextRunAt = null;
        task.LastError = "V3：自动活动、卡池、生日和旧周期玩法任务已停用。";
        task.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<IReadOnlyList<ProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
        => await database.ProviderHealthRecords.AsNoTracking().OrderBy(x => x.ProviderName).Select(x => new ProviderHealthSnapshot(x.ProviderName, x.Status, x.LastSuccessAt, x.LastFailureAt, x.LastLatencyMs, x.ItemCount, x.ParserStatus, x.FailureCount, x.LastError)).ToListAsync(cancellationToken);

    public async Task RecordSuccessAsync(string providerName, int itemCount, long latencyMs, string parserStatus = "OK", CancellationToken cancellationToken = default, string status = "HEALTHY")
    {
        var record = await GetOrCreateHealthAsync(providerName, cancellationToken);
        record.Status = status; record.LastSuccessAt = DateTimeOffset.UtcNow; record.LastLatencyMs = latencyMs; record.ItemCount = itemCount; record.ParserStatus = status == "PARTIAL" ? "PARTIAL" : parserStatus; record.FailureCount = 0; record.LastError = null; record.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(string providerName, string status, string error, long latencyMs, CancellationToken cancellationToken = default, int? itemCount = null)
    {
        var record = await GetOrCreateHealthAsync(providerName, cancellationToken);
        record.Status = status; record.LastFailureAt = DateTimeOffset.UtcNow; record.LastLatencyMs = latencyMs; if (itemCount is not null) record.ItemCount = itemCount.Value; record.FailureCount++; record.LastError = error; record.ParserStatus = status == "FAILED" ? "FAILED" : record.ParserStatus; record.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SchedulerTaskSnapshot>> GetTasksAsync(CancellationToken cancellationToken = default)
        => await database.SchedulerTaskRecords.AsNoTracking().OrderBy(x => x.TaskKey).Select(x => new SchedulerTaskSnapshot(x.TaskKey, x.DisplayName, x.ScheduleText, x.Status, x.LastRunAt, x.NextRunAt, x.FailureCount, x.MaxRetries, x.LastError)).ToListAsync(cancellationToken);

    public async Task<int> RunNowAsync(string taskKey, Func<CancellationToken, Task<int>> action, CancellationToken cancellationToken = default)
    {
        var task = await database.SchedulerTaskRecords.SingleAsync(x => x.TaskKey == taskKey, cancellationToken);
        if (SchedulerScheduleCatalog.IsRetiredAutomaticTask(taskKey))
        {
            DisableRetiredAutomaticTask(task);
            await database.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"V3 retired scheduler task '{taskKey}' cannot be run.");
        }
        task.Status = "RUNNING"; task.UpdatedAt = DateTimeOffset.UtcNow; await database.SaveChangesAsync(cancellationToken);
        task.LastError = null;
        for (var attempt = 0; attempt <= task.MaxRetries; attempt++)
        {
            try
            {
                var count = await action(cancellationToken);
                task.Status = "SUCCEEDED"; task.LastRunAt = DateTimeOffset.UtcNow; task.FailureCount = 0; task.UpdatedAt = DateTimeOffset.UtcNow; await database.SaveChangesAsync(cancellationToken); return count;
            }
            catch (Exception ex) when (attempt < task.MaxRetries)
            {
                task.FailureCount++; task.LastError = ex.Message; await database.SaveChangesAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)), cancellationToken);
            }
            catch (Exception ex)
            {
                task.Status = "FAILED"; task.LastError = ex.Message; task.FailureCount++; task.LastRunAt = DateTimeOffset.UtcNow; task.UpdatedAt = DateTimeOffset.UtcNow; await database.SaveChangesAsync(cancellationToken); throw;
            }
        }
        throw new InvalidOperationException("Scheduler retry loop terminated unexpectedly.");
    }

    private async Task<ProviderHealthRecord> GetOrCreateHealthAsync(string providerName, CancellationToken cancellationToken)
    {
        var record = await database.ProviderHealthRecords.SingleOrDefaultAsync(x => x.ProviderName == providerName, cancellationToken);
        if (record is not null) return record;
        record = new ProviderHealthRecord { ProviderName = providerName };
        database.ProviderHealthRecords.Add(record); return record;
    }
}
