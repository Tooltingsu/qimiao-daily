using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class V3MixedDailyReportTests
{
    [Fact]
    public async Task BuildAutomaticSections_MixesFormalV3GameData_AndExcludesLegacyAndUnconfirmedCandidates()
    {
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new QimiaoDailyDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();

        var reportDate = new DateOnly(2026, 8, 21);
        var now = new DateTimeOffset(2026, 8, 21, 1, 0, 0, TimeSpan.Zero);
        database.ManualEvents.Add(new ManualEventEntity
        {
            Game = "GENSHIN", Name = "浜哄伐娲诲姩", StartAt = now.AddHours(2), EndAt = now.AddDays(2),
            Origin = DataOrigin.Manual, UserConfirmed = true
        });
        var banner = new BannerEntity
        {
            Game = "GENSHIN", Name = "浜哄伐鍗℃睜", Type = "涓婂崐鍗℃睜", StartAt = now.AddHours(2), EndAt = now.AddDays(3),
            Origin = DataOrigin.Imported, UserConfirmed = true
        };
        banner.Characters.Add(new BannerCharacterEntity { Name = "角色甲", SortOrder = 0 });
        banner.Characters.Add(new BannerCharacterEntity { Name = "角色乙", SortOrder = 1 });
        database.Banners.Add(banner);

        var rule = new EndgameRuleEntity { Game = "NTE", Name = "杞ㄥ涔嬪", RuleKind = "FIXED_INTERVAL", ConfigurationJson = "{\"timePrecision\":\"DATE_ONLY\"}" };
        database.EndgameRules.Add(rule);
        database.EndgameOccurrences.Add(new EndgameOccurrenceEntity
        {
            RuleId = rule.Id,
            StartAt = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero)
        });

        database.TimelineItems.AddRange(
            Video("已确认视频", now, confirmed: true),
            Video("未确认视频", now, confirmed: false),
            LegacyEvent("旧自动活动", now));
        database.GitCommitRecords.Add(new GitCommitRecord
        {
            Repository = "babalae/better-genshin-impact", Sha = "abcdef123456", Subject = "宸查€夋嫨 BGI", Url = "https://example.invalid/bgi",
            SelectedForReport = true, FetchedAt = now
        });
        database.Artworks.Add(new ArtworkEntity
        {
            Platform = "x", ArtworkId = "v3-art", NormalizedUrl = "https://example.invalid/art", SourceUrl = "https://example.invalid/art",
            Title = "已选择美图", Author = "作者", CharacterName = "美图角色", FranchiseName = "GENSHIN",
            ReviewStatus = ReviewStatus.Confirmed, SelectedForReport = true, PublishedAt = now
        });
        await database.SaveChangesAsync();

        var service = new DailyReportService(database);
        await service.BuildAutomaticSectionsAsync(reportDate);
        var draft = await service.GetOrCreateAsync(reportDate);
        var games = draft.Sections.Single(x => x.Key == "games").Text;

        Assert.Contains("浜哄伐娲诲姩", games);
        Assert.Contains("浜哄伐鍗℃睜", games);
        Assert.Contains("角色甲、角色乙", games);
        Assert.Contains("杞ㄥ涔嬪", games);
        Assert.Contains("今日刷新", games);
        Assert.DoesNotContain("00:00", games);
        Assert.Contains("已确认视频", games);
        Assert.DoesNotContain("未确认视频", games);
        Assert.DoesNotContain("旧自动活动", games);
        Assert.Contains("宸查€夋嫨 BGI", draft.Sections.Single(x => x.Key == "bgi").Text);
        Assert.Contains("美图角色 原神 来源：x", draft.Sections.Single(x => x.Key == "artwork").Text);
    }

    [Fact]
    public void FormatArtworks_UsesCharacterFranchiseAndPlatformOnly()
    {
        var text = DailyReportFormatter.FormatArtworks([
            new ArtworkEntity
            {
                Platform = "PIXIV", ArtworkId = "hutao", Title = "不应写入日报的标题", Author = "不应写入日报的作者",
                CharacterName = "胡桃", FranchiseName = "GENSHIN", ReviewStatus = ReviewStatus.Confirmed,
                SelectedForReport = true, PublishedAt = DateTimeOffset.UtcNow
            }
        ]);

        Assert.Equal("胡桃 原神 来源：pixiv", text);
        Assert.DoesNotContain("不应写入日报", text);
    }

    private static TimelineItem Video(string title, DateTimeOffset publishedAt, bool confirmed)
    {
        var item = new TimelineItem("NTE", "VIDEO", title, VerificationStatus.VerifiedOfficial,
            publishedAt.ToString("O"), "UTC", publishedAt, TimePrecision.Exact, publishedAt);
        item.SetDataProvenance(DataOrigin.AutoCollected, false);
        item.AddEvidence(new EvidenceRecord("official", "video", "https://example.invalid/" + title, title, "test", publishedAt));
        if (confirmed) item.Confirm("tester", "confirmed", publishedAt);
        return item;
    }

    private static TimelineItem LegacyEvent(string title, DateTimeOffset startAt)
    {
        var item = new TimelineItem("GENSHIN", "EVENT", title, VerificationStatus.VerifiedOfficial,
            startAt.ToString("O"), "UTC", startAt, TimePrecision.Exact, startAt);
        item.SetDataProvenance(DataOrigin.LegacyAuto, false);
        item.AddEvidence(new EvidenceRecord("legacy", "event", "https://example.invalid/legacy", title, "test", startAt));
        item.Confirm("tester", "legacy", startAt);
        return item;
    }
}
