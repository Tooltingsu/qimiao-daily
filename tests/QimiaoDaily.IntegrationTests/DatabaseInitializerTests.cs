using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public void EnsureReady_IsIdempotentAndCreatesOperationalTables()
    {
        var root = Path.Combine(Path.GetTempPath(), "qimiao-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new QimiaoDailyPaths(root);
            paths.EnsureDirectories();
            var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={paths.DatabasePath};Pooling=False").Options;
            using (var first = new QimiaoDailyDbContext(options)) QimiaoDatabaseInitializer.EnsureReady(first);
            using (var second = new QimiaoDailyDbContext(options))
            {
                QimiaoDatabaseInitializer.EnsureReady(second);
                Assert.NotNull(second.ProviderHealthRecords);
                Assert.NotNull(second.CalendarEvents);
                Assert.NotNull(second.ReportSections);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
