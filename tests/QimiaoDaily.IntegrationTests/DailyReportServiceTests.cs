using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class DailyReportServiceTests
{
    [Fact]
    public async Task ComposeAsync_UsesFormalDailyReportTitleAndSectionSeparators()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var date = new DateOnly(2026, 8, 10);
        var service = new DailyReportService(db);
        await service.RebuildSectionAsync(date, "calendar", "今天是二十四节气 立秋");
        await service.RebuildSectionAsync(date, "games", "-原神 活动「示例活动」今日10:00开始");

        var report = await service.ComposeAsync(date);

        Assert.StartsWith("绮喵日报 260810" + Environment.NewLine + Environment.NewLine + "今天是2026年8月10日，星期一", report);
        Assert.DoesNotContain("节日与纪念日", report);
        Assert.Contains(Environment.NewLine + "——————————————————" + Environment.NewLine + Environment.NewLine + "游戏活动预览", report);
        Assert.DoesNotContain("BGI 更新", report);
    }

    [Fact]
    public async Task ComposeAsync_KeepsEmptyGamesHeadingWhenThereAreNoReminderNodes()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var date = new DateOnly(2026, 8, 19);
        var service = new DailyReportService(db);
        await service.RebuildSectionAsync(date, "calendar", "- 七夕");
        await service.RebuildSectionAsync(date, "games", string.Empty);

        var report = await service.ComposeAsync(date);

        Assert.Contains("游戏活动预览", report);
        Assert.DoesNotContain("进行中", report);
    }

    [Fact]
    public async Task ManualSection_IsNotOverwrittenUntilRestored()
    {
        var options=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;await using var db=new QimiaoDailyDbContext(options);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();var service=new DailyReportService(db);var date=new DateOnly(2026,8,15);
        Assert.True(await service.RebuildSectionAsync(date,"games","auto-v1"));await service.UpdateManualSectionAsync(date,"games","manual");Assert.False(await service.RebuildSectionAsync(date,"games","auto-v2"));Assert.Contains("manual",await service.ComposeAsync(date));await service.RestoreAutomaticSectionAsync(date,"games");Assert.True(await service.RebuildSectionAsync(date,"games","auto-v3"));Assert.Contains("auto-v3",await service.ComposeAsync(date));
    }
    [Fact]
    public async Task ExportAsync_WritesMarkdownOrText()
    {
        var options=new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;await using var db=new QimiaoDailyDbContext(options);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();var service=new DailyReportService(db);var date=new DateOnly(2026,8,15);await service.RebuildSectionAsync(date,"bgi","- commit (abc123)");var path=Path.Combine(Path.GetTempPath(),"qimiao-report-test.md");await service.ExportAsync(date,path,true);Assert.Contains("# 绮喵日报",await File.ReadAllTextAsync(path));File.Delete(path);
    }
    [Fact]
    public async Task Section_DeleteMoveAndRestore_ArePersisted()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options; await using var db = new QimiaoDailyDbContext(options); await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var service = new DailyReportService(db); var date = new DateOnly(2026, 8, 15); await service.RebuildSectionAsync(date, "calendar", "calendar"); await service.RebuildSectionAsync(date, "games", "games");
        await service.MoveSectionAsync(date, "games", -1); var composed = await service.ComposeAsync(date); Assert.Contains("游戏活动预览", composed); Assert.Contains("calendar", composed); Assert.True(composed.IndexOf("游戏活动预览", StringComparison.Ordinal) < composed.IndexOf("calendar", StringComparison.Ordinal));
        await service.DeleteSectionAsync(date, "games"); Assert.DoesNotContain("games", await service.ComposeAsync(date));
        await service.RestoreAutomaticSectionAsync(date, "games"); Assert.True(await service.RebuildSectionAsync(date, "games", "restored")); Assert.Contains("restored", await service.ComposeAsync(date));
    }

    [Fact]
    public async Task BuildAutomaticSectionAsync_RebuildsOnlyRequestedSectionAndRestoresDeletedContent()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var date = new DateOnly(2026, 8, 15);
        db.GitCommitRecords.Add(new GitCommitRecord
        {
            Repository = "babalae/better-genshin-impact",
            Sha = "abcdef1234567",
            Subject = "自动生成的提交",
            Url = "https://example.invalid/commit",
            SelectedForReport = true,
            FetchedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new DailyReportService(db);
        await service.UpdateManualSectionAsync(date, "games", "手工游戏内容");
        Assert.False(await service.BuildAutomaticSectionAsync(date, "games"));
        Assert.Contains("手工游戏内容", await service.ComposeAsync(date));

        Assert.True(await service.BuildAutomaticSectionAsync(date, "bgi"));
        Assert.Contains("自动生成的提交", await service.ComposeAsync(date));

        await service.DeleteSectionAsync(date, "bgi");
        Assert.False(await service.BuildAutomaticSectionAsync(date, "bgi"));
        await service.RestoreAutomaticSectionAsync(date, "bgi");
        Assert.True(await service.BuildAutomaticSectionAsync(date, "bgi"));
        Assert.Contains("自动生成的提交", await service.ComposeAsync(date));
    }
}
