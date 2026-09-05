using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QimiaoDaily.Data;

public sealed class QimiaoDailyDesignTimeDbContextFactory : IDesignTimeDbContextFactory<QimiaoDailyDbContext>
{
    public QimiaoDailyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new QimiaoDailyDbContext(options);
    }
}
