using QimiaoDaily.Core;

namespace QimiaoDaily.Desktop.Localization;

/// <summary>将存储层的稳定代码转换为桌面端可读的中文名称。</summary>
public static class DisplayNameMapper
{
    // Display-only aliases. ArtworkEntity.Tags always keeps the exact tag returned by Pixiv.
    // Unlisted tags deliberately fall back to their original text, so they are never rejected or lost.
    private static readonly IReadOnlyDictionary<string, string> ArtworkTagNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["\u521d\u97f3\u30df\u30af"] = "\u521d\u97f3\u672a\u6765", ["HatsuneMiku"] = "\u521d\u97f3\u672a\u6765", ["Miku"] = "\u521d\u97f3\u672a\u6765", ["miku"] = "\u521d\u97f3\u672a\u6765",
        ["\u30de\u30b8\u30ab\u30eb\u30df\u30e9\u30a4"] = "\u9b54\u6cd5\u672a\u6765", ["\u30dc\u30ab\u30ed"] = "VOCALOID", ["VOCALOID"] = "VOCALOID",
        ["\u6f2b\u753b"] = "\u6f2b\u753b", ["\u30aa\u30ea\u30b8\u30ca\u30eb"] = "\u539f\u521b", ["\u5275\u4f5c"] = "\u539f\u521b", ["\u30a4\u30e9\u30b9\u30c8"] = "\u63d2\u753b",
        ["\u5973\u306e\u5b50"] = "\u5973\u5b69\u5b50", ["\u6c34\u7740"] = "\u6cf3\u88c5", ["\u5275\u4f5c\u767e\u5408"] = "\u539f\u521b\u767e\u5408", ["\u5275\u4f5c\u7537\u5973"] = "\u539f\u521b\u7537\u5973",
        ["\u30dd\u30b1\u30e2\u30f3"] = "\u5b9d\u53ef\u68a6", ["\u30d6\u30eb\u30fc\u30a2\u30fc\u30ab\u30a4\u30d6"] = "\u78a7\u84dd\u6863\u6848", ["\u30d6\u30eb\u30a2\u30ab"] = "\u78a7\u84dd\u6863\u6848",
        ["\u5d29\u58ca\u30b9\u30bf\u30fc\u30ec\u30a4\u30eb"] = "\u5d29\u574f\uff1a\u661f\u7a79\u94c1\u9053", ["\u6771\u65b9"] = "\u4e1c\u65b9", ["\u6771\u65b9Project"] = "\u4e1c\u65b9Project",
        ["\u30d7\u30ea\u30ad\u30e5\u30a2"] = "\u5149\u4e4b\u7f8e\u5c11\u5973", ["precure"] = "\u5149\u4e4b\u7f8e\u5c11\u5973", ["\u30d0\u30fc\u30c1\u30e3\u30ebYouTuber"] = "\u865a\u62df\u4e3b\u64ad",
        ["\u8db3\u88cf"] = "\u811a\u5e95", ["\u88f8\u8db3"] = "\u8d64\u811a", ["\u8db3\u6307"] = "\u811a\u8dbe", ["\u304a\u5c3b"] = "\u81c0\u90e8", ["\u30cf\u30a4\u30d2\u30fc\u30eb"] = "\u9ad8\u8ddf\u978b",
        ["\u63cf\u304d\u65b9"] = "\u7ed8\u753b\u6559\u7a0b", ["\u670d"] = "\u670d\u88c5", ["\u30d1\u30fc\u30ab\u30fc"] = "\u8fde\u5e3d\u886b", ["\u670d\u306e\u30b7\u30ef"] = "\u8863\u670d\u8910\u7682",
        ["Original"] = "\u539f\u521b", ["OC"] = "\u539f\u521b\u89d2\u8272", ["Commission"] = "\u7ea6\u7a3f", ["slimegirl"] = "\u53f2\u83b1\u59c6\u5a18", ["Foot"] = "\u811a\u90e8"
    };

    private static readonly IReadOnlyDictionary<string, string> BirthdayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Albedo"] = "阿贝多", ["Aloy"] = "埃洛伊", ["Amber"] = "安柏", ["Arataki Itto"] = "荒泷一斗",
        ["Barbara"] = "芭芭拉", ["Beidou"] = "北斗", ["Bennett"] = "班尼特", ["Chongyun"] = "重云",
        ["Diluc"] = "迪卢克", ["Diona"] = "迪奥娜", ["Eula"] = "优菈", ["Fischl"] = "菲谢尔",
        ["Ganyu"] = "甘雨", ["Gorou"] = "五郎", ["Hu Tao"] = "胡桃", ["Jean"] = "琴",
        ["Kaedehara Kazuha"] = "枫原万叶", ["Kaeya"] = "凯亚", ["Kamisato Ayaka"] = "神里绫华", ["Kamisato Ayato"] = "神里绫人",
        ["Keqing"] = "刻晴", ["Klee"] = "可莉", ["Kujou Sara"] = "九条裟罗", ["Lisa"] = "丽莎",
        ["Mona"] = "莫娜", ["Ningguang"] = "凝光", ["Noelle"] = "诺艾尔", ["Qiqi"] = "七七",
        ["Raiden Shogun"] = "雷电将军", ["Razor"] = "雷泽", ["Rosaria"] = "罗莎莉亚", ["Sangonomiya Kokomi"] = "珊瑚宫心海",
        ["Sayu"] = "早柚", ["Shenhe"] = "申鹤", ["Sucrose"] = "砂糖", ["Tartaglia"] = "达达利亚",
        ["Thoma"] = "托马", ["Traveler"] = "旅行者", ["Traveler (Anemo)"] = "旅行者（风）", ["Traveler (Electro)"] = "旅行者（雷）", ["Traveler (Geo)"] = "旅行者（岩）",
        ["Venti"] = "温迪", ["Xiangling"] = "香菱", ["Xiao"] = "魈", ["Xingqiu"] = "行秋", ["Xinyan"] = "辛焱",
        ["Yae Miko"] = "八重神子", ["Yanfei"] = "烟绯", ["Yoimiya"] = "宵宫", ["Yun Jin"] = "云堇", ["Zhongli"] = "钟离",
        ["Ai Hyperion Λ"] = "爱衣·休伯利安Λ", ["Ai Hyperion 托"] = "爱衣·休伯利安Λ", ["Aponia"] = "阿波尼亚", ["Bronya Zaychik"] = "布洛妮娅·扎伊切克", ["Carole Pepper"] = "卡萝尔·佩珀",
        ["Durandal"] = "幽兰黛尔", ["Eden"] = "伊甸", ["Elysia"] = "爱莉希雅", ["Fu Hua"] = "符华", ["Griseo"] = "格蕾修",
        ["Kallen Kaslana"] = "卡莲·卡斯兰娜", ["Kiana Kaslana"] = "琪亚娜·卡斯兰娜", ["Li Sushang"] = "李素裳", ["Liliya Olenyeva"] = "莉莉娅·阿琳耶娃",
        ["Misteln Schariac"] = "丽瑟尔·沙尼亚特", ["Mobius"] = "梅比乌斯", ["Murata Himeko"] = "无量塔姬子", ["Natasha Cioara"] = "娜塔莎·希奥拉",
        ["PROMETHEUS"] = "普罗米修斯", ["Pardofelis"] = "帕朵菲莉丝", ["Raiden Mei"] = "雷电芽衣", ["Rita Rossweisse"] = "丽塔·洛丝薇瑟",
        ["Rozaliya Olenyeva"] = "罗莎莉娅·阿琳耶娃", ["Seele Vollerei"] = "希儿·芙乐艾", ["Shigure Kira"] = "时雨绮罗", ["Sirin"] = "西琳",
        ["Susannah"] = "苏莎娜", ["Theresa Apocalypse"] = "德丽莎·阿波卡利斯", ["Vill-V"] = "维尔薇", ["Yae Sakura"] = "八重樱",
        ["yi"] = "官方角色槽位 01", ["zhen"] = "官方角色槽位 02", ["ka"] = "官方角色槽位 03", ["an"] = "官方角色槽位 04",
        ["xun"] = "官方角色槽位 05", ["zero-male"] = "官方角色槽位 06", ["zero-female"] = "官方角色槽位 07", ["mint"] = "官方角色槽位 08",
        ["nanally"] = "官方角色槽位 09", ["xiaozhi"] = "官方角色槽位 10", ["jiuyuan"] = "官方角色槽位 11", ["hasuoer"] = "官方角色槽位 12",
        ["baicang"] = "官方角色槽位 13", ["fadia"] = "官方角色槽位 14", ["dfde"] = "官方角色槽位 15", ["zaowu"] = "官方角色槽位 16"
    };

    public const string EvidenceLabel = "证据";
    public const string ParserLabel = "解析器";
    public const string TimezoneLabel = "时区";
    public const string RunNowLabel = "立即执行";
    public const string ArchiveLabel = "归档";
    public const string RevisionHistoryLabel = "修订记录";

    public static string Game(string? code) => Normalize(code) switch
    {
        "" => "留空（不指定游戏）",
        "GENSHIN" => "原神",
        "STARRAIL" => "崩坏：星穹铁道",
        "NTE" => "异环",
        "HI3" => "崩坏3",
        "ALL" => "全部游戏",
        _ => code?.Trim() ?? string.Empty
    };

    public static string ItemType(string? code) => Normalize(code) switch
    {
        "EVENT" => "活动",
        "GACHA" => "卡池",
        "ENDGAME" => "周期挑战",
        "VIDEO" => "视频",
        "PREVIEWNOTICE" => "前瞻预告",
        "PREVIEWLIVE" => "前瞻直播",
        "ALL" => "全部类型",
        _ => code?.Trim() ?? string.Empty
    };

    public static string GachaPoolKind(string? code) => Normalize(code) switch
    {
        "CHARACTER" => "\u89d2\u8272\u6c60",
        "SPECIAL" => "\u6b66\u5668\u6c60",
        "LIGHTCONE" => "\u5149\u9525\u6c60",
        "CHRONICLED" => "\u96c6\u5f55\u7948\u613f",
        "UNKNOWN" or "" => "\u5f85\u786e\u8ba4",
        _ => code?.Trim() ?? "\u5f85\u786e\u8ba4"
    };

    public static string GachaPoolPhase(string? code) => Normalize(code) switch
    {
        "FIRSTHALF" => "\u4e0a\u534a",
        "SECONDHALF" => "\u4e0b\u534a",
        "FULLVERSION" => "全版本",
        "UNKNOWN" or "" => "\u5f85\u786e\u8ba4",
        _ => code?.Trim() ?? "\u5f85\u786e\u8ba4"
    };

    public static string CalendarKind(string? code) => Normalize(code) switch
    {
        "BIRTHDAY" => "生日",
        "ANNIVERSARY" => "周年纪念",
        "FESTIVAL" => "传统节日",
        "SOLARTERM" => "二十四节气",
        "MEMORIAL" => "纪念日",
        "GAME" => "游戏事件",
        "ALL" => "全部事件",
        _ => code?.Trim() ?? string.Empty
    };

    public static string ArtworkCategory(string? code) => Normalize(code) switch
    {
        "ILLUST" => "插画",
        "MANGA" => "漫画",
        "UGOIRA" => "动图",
        "VIDEO" => "视频",
        _ => code?.Trim() ?? string.Empty
    };

    public static string ArtworkPlatform(string? code) => Normalize(code) switch
    {
        "PIXIV" => "Pixiv",
        _ => code?.Trim() ?? string.Empty
    };

    public static string ArtworkTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return string.Empty;
        return string.Join("\u3001", tags.Split([',', '\uff0c'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(TranslateArtworkTag));
    }

    private static string TranslateArtworkTag(string tag)
    {
        if (ArtworkTagNames.TryGetValue(tag, out var translated)) return translated;

        var match = System.Text.RegularExpressions.Regex.Match(tag, "^VOCALOID(?<count>\\d+)users\u5165\u308a$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? $"VOCALOID \u6536\u85cf {match.Groups["count"].Value}+" : tag;
    }

    public static string ReviewStatus(string? status) => Normalize(status) switch
    {
        "PENDING" => "待审核",
        "CONFIRMED" => "已确认",
        "RETURNED" => "已退回",
        "ARCHIVED" => "已归档",
        _ => status?.Trim() ?? string.Empty
    };

    public static string Verification(VerificationStatus status) => Verification(status.ToString());

    public static string Verification(string? status) => Normalize(status) switch
    {
        "VERIFIEDOFFICIAL" => "官方已核验",
        "VERIFIEDMULTISOURCE" => "多源已核验",
        "UNVERIFIED" => "待核验",
        "CONFLICT" => "来源冲突",
        _ => status?.Trim() ?? string.Empty
    };

    public static string TimePrecision(string? precision) => Normalize(precision) switch
    {
        "EXACT" => "精确时间",
        "DATEONLY" => "仅日期",
        "RELATIVE" => "相对时间",
        _ => precision?.Trim() ?? string.Empty
    };

    public static string Change(string? change) => Normalize(change) switch
    {
        "NEW" => "新增",
        "TIMECHANGED" => "时间变更",
        "CONTENTCHANGED" => "内容变更",
        "SOURCECHANGED" => "来源变更",
        "CONFLICT" => "来源冲突",
        "NONE" => "无变更",
        _ => change?.Trim() ?? string.Empty
    };

    public static string Task(string? taskKey) => Normalize(taskKey) switch
    {
        "GAMEDATAREFRESH" => "游戏数据刷新",
        "VIDEOREFRESH" => "视频刷新",
        "PREVIEWREFRESH" => "前瞻刷新",
        "ENDGAMEREFRESH" => "周期挑战刷新",
        "GITHUBBGIREFRESH" => "BGI 主仓库刷新",
        "GITHUBSCRIPTSREFRESH" => "BGI 脚本仓库刷新",
        "NTEOFFICIALREFRESH" => "异环官网更新",
        "NTEBILIBILIREFRESH" => "异环 Bilibili 更新",
        "ARTWORKDAILYSEARCH" => "每日美图采集",
        "BIRTHDAYCHARACTERREFRESH" => "角色生日刷新",
        "CALENDARREFRESH" => "日历刷新",
        "ARCHIVECLEANUP" => "归档清理",
        "REPORTBUILD" => "日报生成",
        _ => taskKey?.Trim() ?? string.Empty
    };

    public static string ProviderStatus(string? status) => Normalize(status) switch
    {
        "HEALTHY" => "健康",
        "WARNING" => "警告",
        "FAILED" => "失败",
        "LOGINREQUIRED" => "需要登录",
        "BLOCKED" => "访问受限",
        "IDLE" => "空闲",
        "RUNNING" => "运行中",
        "SUCCEEDED" => "成功",
        "PARTIAL" => "部分成功",
        "NOTRUN" => "尚未运行",
        _ => status?.Trim() ?? string.Empty
    };

    public static string Provider(string? provider)
    {
        var key = Normalize(provider).Replace(":", string.Empty, StringComparison.Ordinal);
        return key switch
        {
            "NTENEVERNESSGGBIRTHDAY" => "异环 Neverness.gg 生日资料",
            "GENSHINOFFICIAL" => "原神官方公告",
            "STARRAILOFFICIAL" => "星铁官方公告",
            "NTEOFFICIALWEBSITE" => "异环官网",
            "NTEOFFICIALROSTER" => "异环官方角色名册",
            "NTEBILIBILIOFFICIAL" => "异环 Bilibili 官方",
            "HONKAI3OFFICIALCHARACTERLIST" => "崩坏3官方角色列表",
            "HI3BILIGAMEBIRTHDAY" => "崩坏3 Biligame 生日资料",
            "HI3BAIDUBIRTHDAY" => "崩坏3 百度百科生日资料",
            "HI3MOEGIRLBIRTHDAY" => "崩坏3 萌娘百科生日资料",
            "BIRTHDAYHOYOWIKI" => "HoYoWiki 生日资料",
            "NTEFANDOMBIRTHDAY" => "异环第三方生日资料",
            "NTEGAMEBIRTHDAY" => "异环 NTEGame 生日资料",
            "OFFICIALYOUTUBERSS" => "官方视频 RSS",
            "OFFICIALYOUTUBERSSGENSHIN" => "原神官方视频",
            "OFFICIALYOUTUBERSSSTARRAIL" => "星铁官方视频",
            "PIXIV" => "Pixiv 美图",
            "BGIGITHUB" => "BGI GitHub",
            _ => provider?.Trim() ?? string.Empty
        };
    }

    public static string ParserStatus(string? status) => Normalize(status) switch
    {
        "NOTRUN" => "尚未运行",
        "OK" => "正常",
        "COVERAGE" => "覆盖率已记录",
        "UNKNOWN" => "未知",
        "READY" => "已就绪",
        "BIRTHDAYCOVERAGE" => "生日覆盖已记录",
        "PARTIAL" => "部分成功",
        "FAILED" => "失败",
        _ => status?.Trim() ?? string.Empty
    };

    public static string RevisionField(string? field) => Normalize(field) switch
    {
        "GAMECODE" => "游戏",
        "ITEMTYPE" => "类型",
        "TITLE" => "标题",
        "SOURCETIME" => "原始时间",
        "SOURCETIMEZONE" => "来源时区",
        "STARTAT" or "NORMALIZEDTIME" => "开始时间",
        "ENDAT" => "结束时间",
        "TIMEPRECISION" => "时间精度",
        "STARTTIMEPRECISION" => "开始时间精度",
        "ENDTIMEPRECISION" => "结束时间精度",
        "STARTTIMESOURCE" => "开始时间来源",
        "ENDTIMESOURCE" => "结束时间来源",
        "STARTEXPRESSION" => "开始时间表达式",
        "ENDEXPRESSION" => "结束时间表达式",
        "STARTTIMEEVIDENCEKEY" => "开始时间证据",
        "ENDTIMEEVIDENCEKEY" => "结束时间证据",
        "REVIEWSTATUS" => "审核状态",
        "VERIFICATIONSTATUS" => "核验状态",
        "CHANGEKIND" => "变更类型",
        "CANONICALIDENTITY" => "标准标识",
        "ARTWORK" => "美图",
        "CHARACTERNAME" => "角色名",
        "FRANCHISENAME" => "系列",
        "CATEGORY" => "分类",
        "TAGS" => "标签",
        _ => string.IsNullOrWhiteSpace(field) ? "字段" : field.Trim()
    };

    public static string RevisionValue(string? field, string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0) return "空值";
        return Normalize(field) switch
        {
            "REVIEWSTATUS" => ReviewStatus(text),
            "VERIFICATIONSTATUS" => Verification(text),
            "TIMEPRECISION" or "STARTTIMEPRECISION" or "ENDTIMEPRECISION" => TimePrecision(text),
            "CHANGEKIND" => Change(text),
            _ => text
        };
    }

    public static string RevisionReason(string? reason)
    {
        var text = reason?.Trim() ?? string.Empty;
        if (text.Length == 0) return "未填写原因";
        return Normalize(text).Replace(" ", string.Empty, StringComparison.Ordinal) switch
        {
            "DESKTOPARTWORKREVIEWOPERATION" => "桌面端美图审核操作",
            "DESKTOPARTWORKCONFIRMATION" => "桌面端确认美图",
            "DESKTOPARTWORKREVIEWRETURN" => "桌面端退回美图审核",
            "MANUALEDIT" => "手工编辑",
            _ => text
        };
    }

    public static string ProviderError(string? error)
        => (error ?? string.Empty)
            .Replace("Bilibili API code", "Bilibili 接口代码", StringComparison.OrdinalIgnoreCase)
            .Replace("Bilibili API access is blocked.", "Bilibili 接口访问受限。", StringComparison.OrdinalIgnoreCase)
            .Replace("Biligame request failed: ", "Biligame 请求失败：", StringComparison.OrdinalIgnoreCase)
            .Replace("Biligame request failed:", "Biligame 请求失败：", StringComparison.OrdinalIgnoreCase)
            .Replace("Baidu request failed: ", "百度请求失败：", StringComparison.OrdinalIgnoreCase)
            .Replace("Baidu request failed:", "百度请求失败：", StringComparison.OrdinalIgnoreCase)
            .Replace("HTTP status code", "HTTP 状态码", StringComparison.OrdinalIgnoreCase)
            .Replace("The request was canceled due to the configured HttpClient.Timeout of 20 seconds elapsing.", "请求超时（20秒）。", StringComparison.OrdinalIgnoreCase)
            .Replace("The request was canceled due to the configured HttpClient.Timeout of 20 seconds elapsing", "请求超时（20秒）", StringComparison.OrdinalIgnoreCase)
            .Replace("Pixiv session required.", "需要配置 Pixiv Session。", StringComparison.OrdinalIgnoreCase)
            .Replace("Session is not configured.", "尚未配置 Session。", StringComparison.OrdinalIgnoreCase)
            .Replace("Official NTE roster fetch failed or was incomplete; using audited 16-slot fallback.", "异环官方角色名册获取失败或不完整；使用已审计的16个角色槽位回退。", StringComparison.OrdinalIgnoreCase)
            .Replace("NTEGame single-source birthday candidate; pending second-source verification.", "异环 NTEGame 单一来源生日候选；等待第二来源核验。", StringComparison.OrdinalIgnoreCase)
            .Replace("Pixiv requires an authorized session for daily ranking.", "Pixiv 需要已授权会话才能获取每日排行。", StringComparison.OrdinalIgnoreCase)
            .Replace("Pixiv requires an authorized session for this artwork.", "Pixiv 需要已授权会话才能获取该作品。", StringComparison.OrdinalIgnoreCase)
            .Replace("Pixiv temporarily blocked or rate-limited daily ranking.", "Pixiv 暂时阻止或限制了每日排行请求。", StringComparison.OrdinalIgnoreCase)
            .Replace("Pixiv temporarily blocked or rate-limited the request.", "Pixiv 暂时阻止或限制了该请求。", StringComparison.OrdinalIgnoreCase)
            .Replace("YouTube RSS request failed after 3 attempts.", "YouTube RSS 请求失败，已重试3次。", StringComparison.OrdinalIgnoreCase)
            .Replace("YouTube RSS request failed.", "YouTube RSS 请求失败。", StringComparison.OrdinalIgnoreCase)
            .Replace("All official video sources failed.", "所有官方视频来源均失败。", StringComparison.OrdinalIgnoreCase)
            .Replace("Honkai 3 official character API returned an error.", "崩坏3官方角色接口返回错误。", StringComparison.OrdinalIgnoreCase)
            .Replace("GENSHIN ", "原神 ", StringComparison.OrdinalIgnoreCase)
            .Replace("STARRAIL ", "崩坏：星穹铁道 ", StringComparison.OrdinalIgnoreCase)
            .Replace("NTE ", "异环 ", StringComparison.OrdinalIgnoreCase);

    public static string BirthdayCharacter(string? canonicalName, string? aliases = null)
    {
        var name = canonicalName?.Trim() ?? string.Empty;
        if (name.Length == 0) return aliases?.Trim() ?? string.Empty;
        return BirthdayNames.TryGetValue(name, out var chinese) ? chinese : name;
    }

    public static string Auto(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        foreach (var mapped in new[] { Game(text), ItemType(text), CalendarKind(text), ArtworkCategory(text), ArtworkPlatform(text), ReviewStatus(text), Verification(text), TimePrecision(text), Task(text), ProviderStatus(text), ParserStatus(text), Change(text) })
            if (!string.Equals(mapped, text, StringComparison.OrdinalIgnoreCase)) return mapped;
        return text;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
}
