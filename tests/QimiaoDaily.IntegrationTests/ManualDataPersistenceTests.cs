using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class ManualDataPersistenceTests
{
    [Fact]
    public async Task BannerCharacters_ArePersistedWithTheirExplicitOrder()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var banner = new BannerEntity { Game = "GENSHIN", Name = "月之一", Type = "上半卡池", StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddDays(21), Origin = DataOrigin.Manual, UserConfirmed = true };
        banner.Characters.Add(new BannerCharacterEntity { Name = "哥伦比娅", SortOrder = 0 });
        banner.Characters.Add(new BannerCharacterEntity { Name = "雷电将军", SortOrder = 1 });
        database.Banners.Add(banner);
        await database.SaveChangesAsync();

        var saved = await database.Banners.Include(x => x.Characters).SingleAsync();
        Assert.Equal(["哥伦比娅", "雷电将军"], saved.Characters.OrderBy(x => x.SortOrder).Select(x => x.Name));
    }
}
