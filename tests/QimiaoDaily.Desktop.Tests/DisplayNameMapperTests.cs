using QimiaoDaily.Desktop.Localization;

namespace QimiaoDaily.Desktop.Tests;

public sealed class DisplayNameMapperTests
{
    [Theory]
    [InlineData("GENSHIN", "原神")]
    [InlineData("STARRAIL", "崩坏：星穹铁道")]
    [InlineData("NTE", "异环")]
    public void GameCodes_ArePresentedInChinese(string code, string expected)
        => Assert.Equal(expected, DisplayNameMapper.Game(code));

    [Theory]
    [InlineData("EVENT", "活动")]
    [InlineData("GACHA", "卡池")]
    [InlineData("PREVIEW_NOTICE", "前瞻预告")]
    [InlineData("PREVIEW_LIVE", "前瞻直播")]
    public void ItemTypes_ArePresentedInChinese(string code, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ItemType(code));

    [Fact]
    public void StatusesAndTasks_DoNotLeakInternalEnglishLabels()
    {
        Assert.Equal("待审核", DisplayNameMapper.ReviewStatus("Pending"));
        Assert.Equal("官方已核验", DisplayNameMapper.Verification("VerifiedOfficial"));
        Assert.Equal("游戏数据刷新", DisplayNameMapper.Task("game_data_refresh"));
        Assert.Equal("异环官网更新", DisplayNameMapper.Task("nte_official_refresh"));
        Assert.Equal("异环 Bilibili 更新", DisplayNameMapper.Task("nte_bilibili_refresh"));
        Assert.Equal("健康", DisplayNameMapper.ProviderStatus("HEALTHY"));
    }

    [Theory]
    [InlineData("GenshinOfficial", "原神官方公告")]
    [InlineData("StarRailOfficial", "星铁官方公告")]
    [InlineData("NteOfficialWebsite", "异环官网")]
    [InlineData("NteOfficialRoster", "异环官方角色名册")]
    [InlineData("NteFandomBirthday", "异环第三方生日资料")]
    [InlineData("NteGameBirthday", "异环 NTEGame 生日资料")]
    [InlineData("NteNevernessGgBirthday", "异环 Neverness.gg 生日资料")]
    [InlineData("Hi3MoegirlBirthday", "崩坏3 萌娘百科生日资料")]
    [InlineData("Hi3BiligameBirthday", "崩坏3 Biligame 生日资料")]
    [InlineData("Hi3BaiduBirthday", "崩坏3 百度百科生日资料")]
    public void ProviderNames_ArePresentedInChinese(string provider, string expected)
        => Assert.Equal(expected, DisplayNameMapper.Provider(provider));

    [Theory]
    [InlineData("NOT_RUN", "尚未运行")]
    [InlineData("OK", "正常")]
    [InlineData("COVERAGE", "覆盖率已记录")]
    [InlineData("UNKNOWN", "未知")]
    [InlineData("READY", "已就绪")]
    [InlineData("BIRTHDAY_COVERAGE", "生日覆盖已记录")]
    [InlineData("FAILED", "失败")]
    public void ParserStatuses_ArePresentedInChinese(string status, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ParserStatus(status));

    [Fact]
    public void ProviderErrors_LocalizeKnownTechnicalPhrases()
        => Assert.Equal("Bilibili 接口代码 -799", DisplayNameMapper.ProviderError("Bilibili API code -799"));

    [Fact]
    public void ProviderErrors_LocalizeTimeout()
        => Assert.Equal("请求超时（20秒）。", DisplayNameMapper.ProviderError("The request was canceled due to the configured HttpClient.Timeout of 20 seconds elapsing."));

    [Theory]
    [InlineData("PENDING", "待审核")]
    [InlineData("CONFIRMED", "已确认")]
    [InlineData("NONE", "无变更")]
    [InlineData("SUCCEEDED", "成功")]
    [InlineData("FAILED", "失败")]
    public void Auto_LocalizesReviewChangeAndOperationCodes(string code, string expected)
        => Assert.Equal(expected, DisplayNameMapper.Auto(code));

    [Theory]
    [InlineData("Bilibili API access is blocked.", "Bilibili 接口访问受限。")]
    [InlineData("Biligame request failed: timeout", "Biligame 请求失败：timeout")]
    [InlineData("Baidu request failed: timeout", "百度请求失败：timeout")]
    public void ProviderErrors_LocalizeProviderFailureMessages(string source, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ProviderError(source));

    [Theory]
    [InlineData("Official NTE roster fetch failed or was incomplete; using audited 16-slot fallback.", "异环官方角色名册获取失败或不完整；使用已审计的16个角色槽位回退。")]
    [InlineData("NTEGame single-source birthday candidate; pending second-source verification.", "异环 NTEGame 单一来源生日候选；等待第二来源核验。")]
    [InlineData("Pixiv requires an authorized session for daily ranking.", "Pixiv 需要已授权会话才能获取每日排行。")]
    [InlineData("Pixiv requires an authorized session for this artwork.", "Pixiv 需要已授权会话才能获取该作品。")]
    [InlineData("Pixiv temporarily blocked or rate-limited daily ranking.", "Pixiv 暂时阻止或限制了每日排行请求。")]
    [InlineData("Pixiv temporarily blocked or rate-limited the request.", "Pixiv 暂时阻止或限制了该请求。")]
    [InlineData("YouTube RSS request failed.", "YouTube RSS 请求失败。")]
    [InlineData("YouTube RSS request failed after 3 attempts.", "YouTube RSS 请求失败，已重试3次。")]
    [InlineData("All official video sources failed.", "所有官方视频来源均失败。")]
    [InlineData("Honkai 3 official character API returned an error.", "崩坏3官方角色接口返回错误。")]
    public void ProviderErrors_LocalizeCollectorFailureMessages(string source, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ProviderError(source));

    [Fact]
    public void ProviderErrors_LocalizeGameCodesInCoverageWarnings()
    {
        var warning = DisplayNameMapper.ProviderError("GENSHIN 解析覆盖率 45%，丢弃 17 条，可能漏采；请检查来源健康。");

        Assert.Contains("原神", warning);
        Assert.DoesNotContain("GENSHIN", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Thoma", "托马")]
    [InlineData("Diona", "迪奥娜")]
    [InlineData("Rosaria", "罗莎莉亚")]
    [InlineData("Beidou", "北斗")]
    [InlineData("Yae Sakura", "八重樱")]
    public void BirthdayCharacters_UseCanonicalChineseNames(string source, string expected)
        => Assert.Equal(expected, DisplayNameMapper.BirthdayCharacter(source));

    [Theory]
    [InlineData("yi", "官方角色槽位 01")]
    [InlineData("zero-female", "官方角色槽位 07")]
    [InlineData("zaowu", "官方角色槽位 16")]
    public void NteRosterAliases_UseSafeChinesePlaceholders(string source, string expected)
        => Assert.Equal(expected, DisplayNameMapper.BirthdayCharacter(source));

    [Fact]
    public void UserFacingLabels_DoNotContainKnownEnglishOperatorWords()
    {
        var labels = new[]
        {
            DisplayNameMapper.EvidenceLabel,
            DisplayNameMapper.ParserLabel,
            DisplayNameMapper.TimezoneLabel,
            DisplayNameMapper.RunNowLabel,
            DisplayNameMapper.ArchiveLabel,
            DisplayNameMapper.RevisionHistoryLabel
        };

        Assert.All(labels, label => Assert.DoesNotContain(label, new[] { "Evidence", "Parser", "Timezone", "Run Now", "ARCHIVE", "Revision History" }));
    }

    [Theory]
    [InlineData("BIRTHDAY", "生日")]
    [InlineData("ANNIVERSARY", "周年纪念")]
    [InlineData("FESTIVAL", "传统节日")]
    [InlineData("SOLAR_TERM", "二十四节气")]
    [InlineData("MEMORIAL", "纪念日")]
    [InlineData("GAME", "游戏事件")]
    public void CalendarKinds_ArePresentedInChinese(string kind, string expected)
        => Assert.Equal(expected, DisplayNameMapper.CalendarKind(kind));

    [Theory]
    [InlineData("ILLUST", "插画")]
    [InlineData("MANGA", "漫画")]
    [InlineData("UGOIRA", "动图")]
    public void ArtworkCategories_ArePresentedInChinese(string category, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ArtworkCategory(category));

    [Fact]
    public void ArtworkTags_TranslateKnownTagsAndKeepUnknownTags()
        => Assert.Equal("\u521d\u97f3\u672a\u6765\u3001\u9b54\u6cd5\u672a\u6765\u3001VOCALOID \u6536\u85cf 1000+\u3001custom-tag", DisplayNameMapper.ArtworkTags("\u521d\u97f3\u30df\u30af, \u30de\u30b8\u30ab\u30eb\u30df\u30e9\u30a4, VOCALOID1000users\u5165\u308a, custom-tag"));

    [Theory]
    [InlineData("SUCCEEDED", "成功")]
    [InlineData("PARTIAL", "部分成功")]
    [InlineData("NOT_RUN", "尚未运行")]
    public void OperationalStatuses_ArePresentedInChinese(string status, string expected)
        => Assert.Equal(expected, DisplayNameMapper.ProviderStatus(status));

    [Theory]
    [InlineData("StartAt", "开始时间")]
    [InlineData("EndAt", "结束时间")]
    [InlineData("VerificationStatus", "核验状态")]
    public void RevisionFields_ArePresentedInChinese(string field, string expected)
        => Assert.Equal(expected, DisplayNameMapper.RevisionField(field));

    [Theory]
    [InlineData("ReviewStatus", "Pending", "待审核")]
    [InlineData("ReviewStatus", "Confirmed", "已确认")]
    [InlineData("TimePrecision", "DateOnly", "仅日期")]
    public void RevisionValues_UseFieldSpecificChineseLabels(string field, string value, string expected)
        => Assert.Equal(expected, DisplayNameMapper.RevisionValue(field, value));

    [Theory]
    [InlineData("Desktop artwork review operation", "桌面端美图审核操作")]
    [InlineData("manual edit", "手工编辑")]
    public void RevisionReasons_ArePresentedInChinese(string reason, string expected)
        => Assert.Equal(expected, DisplayNameMapper.RevisionReason(reason));
}
