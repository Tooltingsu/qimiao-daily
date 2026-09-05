namespace QimiaoDaily.Collectors;

public static class BirthdayCharacterNameMap
{
    private static readonly IReadOnlyDictionary<string, string> EnglishToChinese = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ai Hyperion Λ"] = "爱衣·休伯利安Λ", ["Aponia"] = "阿波尼亚", ["Bronya Zaychik"] = "布洛妮娅·扎伊切克", ["Carole Pepper"] = "卡萝尔·佩珀",
        ["Durandal"] = "幽兰黛尔", ["Eden"] = "伊甸", ["Elysia"] = "爱莉希雅", ["Fischl"] = "菲谢尔", ["Fu Hua"] = "符华", ["Griseo"] = "格蕾修",
        ["Kallen Kaslana"] = "卡莲·卡斯兰娜", ["Kiana Kaslana"] = "琪亚娜·卡斯兰娜", ["Li Sushang"] = "李素裳", ["Liliya Olenyeva"] = "莉莉娅·阿琳耶娃",
        ["Misteln Schariac"] = "丽瑟尔·沙尼亚特", ["Mobius"] = "梅比乌斯", ["Murata Himeko"] = "无量塔姬子", ["Natasha Cioara"] = "娜塔莎·希奥拉",
        ["PROMETHEUS"] = "普罗米修斯", ["Pardofelis"] = "帕朵菲莉丝", ["Raiden Mei"] = "雷电芽衣", ["Rita Rossweisse"] = "丽塔·洛丝薇瑟",
        ["Rozaliya Olenyeva"] = "罗莎莉娅·阿琳耶娃", ["Seele Vollerei"] = "希儿·芙乐艾", ["Shigure Kira"] = "时雨绮罗", ["Sirin"] = "西琳",
        ["Susannah"] = "苏莎娜", ["Theresa Apocalypse"] = "德丽莎·阿波卡利斯", ["Vill-V"] = "维尔薇", ["Yae Sakura"] = "八重樱",
        ["Thoma"] = "托马", ["Beidou"] = "北斗"
        , ["yi"] = "官方角色槽位 01", ["zhen"] = "官方角色槽位 02", ["ka"] = "官方角色槽位 03", ["an"] = "官方角色槽位 04"
        , ["xun"] = "官方角色槽位 05", ["zero-male"] = "官方角色槽位 06", ["zero-female"] = "官方角色槽位 07", ["mint"] = "官方角色槽位 08"
        , ["nanally"] = "官方角色槽位 09", ["xiaozhi"] = "官方角色槽位 10", ["jiuyuan"] = "官方角色槽位 11", ["hasuoer"] = "官方角色槽位 12"
        , ["baicang"] = "官方角色槽位 13", ["fadia"] = "官方角色槽位 14", ["dfde"] = "官方角色槽位 15", ["zaowu"] = "官方角色槽位 16"
    };

    public static string Resolve(string name)
        => EnglishToChinese.TryGetValue(name.Trim(), out var value) ? value : name.Trim();
}
