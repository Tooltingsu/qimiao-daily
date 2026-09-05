using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// Runs registered scheduler handlers in the background. Tasks without a handler stay visible in the UI
/// and are intentionally skipped rather than being reported as successful.
public sealed class SchedulerBackgroundService : IAsyncDisposable
{
    private readonly Func<QimiaoDailyDbContext> _databaseFactory;
    private readonly Func<QimiaoDailyDbContext, string, CancellationToken, Task<int>> _handler;
    private readonly SchedulerScheduleCatalog _scheduleCatalog;
    private readonly Timer _timer;
    private int _running;

    public SchedulerBackgroundService(
        Func<QimiaoDailyDbContext> databaseFactory,
        Func<QimiaoDailyDbContext, string, CancellationToken, Task<int>> handler)
        : this(databaseFactory, handler, SchedulerScheduleCatalog.Default)
    {
    }

    public SchedulerBackgroundService(
        Func<QimiaoDailyDbContext> databaseFactory,
        Func<QimiaoDailyDbContext, string, CancellationToken, Task<int>> handler,
        SchedulerScheduleCatalog scheduleCatalog)
    {
        _databaseFactory = databaseFactory;
        _handler = handler;
        _scheduleCatalog = scheduleCatalog;
        _timer = new Timer(static state => _ = ((SchedulerBackgroundService)state!).RunOnceAsync(), this,
            TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(1));
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync();
    }

    public Task RunOnceAsync(CancellationToken cancellationToken = default) => TickAsync(cancellationToken);

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0) return;
        try
        {
            await using var database = _databaseFactory();
            var now = DateTimeOffset.UtcNow;
            var tasks = await database.SchedulerTaskRecords.ToListAsync(cancellationToken);
            foreach (var task in tasks)
            {
                if (SchedulerScheduleCatalog.IsRetiredAutomaticTask(task.TaskKey))
                {
                    OperationsService.DisableRetiredAutomaticTask(task);
                    continue;
                }
                if (!HasHandler(task.TaskKey)) continue;
                if (task.NextRunAt is null)
                {
                    task.NextRunAt = _scheduleCatalog.NextRun(task.TaskKey, now);
                    continue;
                }
                if (task.NextRunAt > now) continue;

                var operations = new OperationsService(database);
                try
                {
                    await operations.RunNowAsync(task.TaskKey, ct => _handler(database, task.TaskKey, ct), cancellationToken);
                }
                catch
                {
                    // RunNowAsync persists the failure state. Keep the timer alive for later retries.
                }
                task.NextRunAt = _scheduleCatalog.NextRun(task.TaskKey, now);
            }
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // A transient database or network issue must not terminate the desktop process.
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private static bool HasHandler(string taskKey) => SchedulerScheduleCatalog.IsScheduledTask(taskKey);

}
