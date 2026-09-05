using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

var paths = new QimiaoDailyPaths();
var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={paths.DatabasePath}").Options;
await using var database = new QimiaoDailyDbContext(options);
var rows = await database.Birthdays.AsNoTracking()
    .OrderBy(x => x.Franchise)
    .ThenBy(x => x.Month == 0 ? 13 : x.Month)
    .ThenBy(x => x.Day)
    .ThenBy(x => x.Character)
    .Select(x => new BirthdayRosterRow(
        x.Franchise, x.Character, x.CanonicalCharacterNameZhCn, x.Aliases, x.Month, x.Day,
        x.Month > 0 && x.Day > 0 ? "KNOWN" : "UNKNOWN",
        x.VerificationStatus.ToString(), x.Enabled, string.Empty, x.Source, x.SourceUrl, x.Evidence))
    .ToListAsync();
rows = rows.Select(x => x with { RecordClass = Classify(x) }).ToList();

var outputRoot = Path.Combine(Environment.GetEnvironmentVariable("QIMIAO_ROSTER_OUTPUT") ?? Path.Combine(Environment.CurrentDirectory, "artifacts"), "birthday-roster-review-20260820");
Directory.CreateDirectory(outputRoot);
var csvPath = Path.Combine(outputRoot, "birthday-roster.csv");
var jsonPath = Path.Combine(outputRoot, "birthday-roster.json");
var mdPath = Path.Combine(outputRoot, "README.md");

await File.WriteAllTextAsync(csvPath, Csv(rows), new UTF8Encoding(false));
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
await File.WriteAllTextAsync(mdPath, Markdown(rows, database.Database.GetDbConnection().DataSource), new UTF8Encoding(false));
Console.WriteLine($"Rows={rows.Count}; Output={outputRoot}");

static string Csv(IEnumerable<BirthdayRosterRow> rows)
{
    var lines = new List<string> { "游戏,角色,中文名,别名,月份,日期,生日状态,核验状态,日报启用,清单分类,来源,来源URL,证据" };
    lines.AddRange(rows.Select(x => string.Join(",", new[] { x.Franchise, x.Character, x.ChineseName, x.Aliases, x.Month.ToString(), x.Day.ToString(), x.BirthdayStatus, x.VerificationStatus, x.Enabled ? "是" : "否", x.RecordClass, x.Source, x.SourceUrl, x.Evidence }.Select(CsvCell))));
    return string.Join(Environment.NewLine, lines) + Environment.NewLine;
}

static string CsvCell(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

static string Markdown(IReadOnlyList<BirthdayRosterRow> rows, string databasePath)
{
    var builder = new StringBuilder();
    builder.AppendLine("# 生日角色清单审阅材料");
    builder.AppendLine();
    builder.AppendLine($"数据库：`{databasePath}`");
    builder.AppendLine();
    builder.AppendLine("本表先确认角色清单，再逐个确认生日。`UNKNOWN` 保留角色但不进入生日日报；`KNOWN` 也必须看核验状态和来源后才能启用。");
    builder.AppendLine();
    builder.AppendLine("| 游戏 | 当前记录 | 可视为角色 | 已知生日 | UNKNOWN | 已启用日报 |");
    builder.AppendLine("|---|---:|---:|---:|---:|---:|");
    foreach (var group in rows.GroupBy(x => x.Franchise).OrderBy(x => x.Key))
        builder.AppendLine($"| {group.Key} | {group.Count()} | {group.Count(x => x.RecordClass != "NON_CHARACTER_CANDIDATE")} | {group.Count(x => x.BirthdayStatus == "KNOWN")} | {group.Count(x => x.BirthdayStatus == "UNKNOWN")} | {group.Count(x => x.Enabled)} |");
    builder.AppendLine();
    builder.AppendLine("## 审阅规则");
    builder.AppendLine();
    builder.AppendLine("- 角色清单以正式数据库当前记录为准，重复角色按数据库唯一键保留一行。");
    builder.AppendLine("- `ROSTER_SLOT_UNKNOWN` 表示官方角色槽位已存在，但官方未公开生日或中文名，必须由用户补证/命名。");
    builder.AppendLine("- `NON_CHARACTER_CANDIDATE` 表示抓取到的分类页等非角色条目，不计入角色完整性，需后续清理或归档。");
    builder.AppendLine("- UNKNOWN 行已高亮语义标记，生日月份/日期为 0，且不得启用。");
    builder.AppendLine("- 请先审阅 CSV/JSON 中的角色是否完整，再逐行补充 UNKNOWN 的生日证据。");
    builder.AppendLine("- 本次只读导出，不修改数据库。");
    return builder.ToString();
}

static string Classify(BirthdayRosterRow item)
{
    if (item.Character.StartsWith("Category:", StringComparison.OrdinalIgnoreCase)) return "NON_CHARACTER_CANDIDATE";
    if (item.Source.Contains("OfficialRoster", StringComparison.OrdinalIgnoreCase) || item.Character.StartsWith("官方角色槽位", StringComparison.Ordinal)) return "ROSTER_SLOT_UNKNOWN";
    return "BIRTHDAY_RECORD";
}

record BirthdayRosterRow(string Franchise, string Character, string ChineseName, string Aliases, int Month, int Day, string BirthdayStatus, string VerificationStatus, bool Enabled, string RecordClass, string Source, string SourceUrl, string Evidence);
