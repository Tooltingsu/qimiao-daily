using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.IntegrationTests;

public sealed class StructuredGachaPersistenceTests
{
    [Fact]
    public async Task GachaItem_PersistsStructuredFields()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        await using var db = new QimiaoDailyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var item = new TimelineItem("GENSHIN", "GACHA", "测试卡池", VerificationStatus.Unverified,
            null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow,
            gachaPoolKind: "CHARACTER", gachaPoolPhase: "FIRST_HALF", gachaGroupKey: "v6.0-p1");
        db.TimelineItems.Add(item);
        await db.SaveChangesAsync();

        var loaded = await db.TimelineItems.SingleAsync();
        Assert.Equal("CHARACTER", loaded.GachaPoolKind);
        Assert.Equal("FIRST_HALF", loaded.GachaPoolPhase);
        Assert.Equal("v6.0-p1", loaded.GachaGroupKey);
    }

    [Fact]
    public void Edit_RecordsStructuredGachaFieldRevisions()
    {
        var item = new TimelineItem("GENSHIN", "GACHA", "测试卡池", VerificationStatus.Unverified,
            null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow,
            gachaPoolKind: "CHARACTER", gachaPoolPhase: "FIRST_HALF", gachaGroupKey: "old");
        var changes = item.Edit("GACHA", "测试卡池", VerificationStatus.Unverified,
            null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow, null,
            gachaPoolKind: "SPECIAL", gachaPoolPhase: "SECOND_HALF", gachaGroupKey: "new");

        Assert.Contains(changes, x => x.FieldName == "GachaPoolKind" && x.OldValue == "CHARACTER" && x.NewValue == "SPECIAL");
        Assert.Contains(changes, x => x.FieldName == "GachaPoolPhase" && x.OldValue == "FIRST_HALF" && x.NewValue == "SECOND_HALF");
        Assert.Contains(changes, x => x.FieldName == "GachaGroupKey" && x.OldValue == "old" && x.NewValue == "new");
    }

    [Fact]
    public void NonGachaItem_KeepsStructuredFieldsNull()
    {
        var item = new TimelineItem("GENSHIN", "EVENT", "普通活动", VerificationStatus.Unverified,
            null, null, null, TimePrecision.DateOnly, DateTimeOffset.UtcNow,
            gachaPoolKind: "CHARACTER", gachaPoolPhase: "FIRST_HALF", gachaGroupKey: "ignored");

        Assert.Null(item.GachaPoolKind);
        Assert.Null(item.GachaPoolPhase);
        Assert.Null(item.GachaGroupKey);
    }
}
