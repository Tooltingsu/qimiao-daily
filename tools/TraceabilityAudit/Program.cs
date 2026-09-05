using System.Text;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;

var databasePath = Environment.GetEnvironmentVariable("QIMIAO_TRACEABILITY_DB")
    ?? throw new InvalidOperationException("QIMIAO_TRACEABILITY_DB is required.");
var outputPath = Environment.GetEnvironmentVariable("QIMIAO_TRACEABILITY_OUTPUT")
    ?? Path.Combine(Environment.CurrentDirectory, "report-traceability.md");
var reportDate = DateOnly.TryParse(Environment.GetEnvironmentVariable("QIMIAO_TRACEABILITY_DATE"), out var parsed)
    ? parsed
    : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").Date);

var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={databasePath}").Options;
await using var database = new QimiaoDailyDbContext(options);
var items = await database.TimelineItems.AsNoTracking().Include(x => x.Evidence)
    .Where(x => x.ReviewStatus == ReviewStatus.Confirmed)
    .OrderBy(x => x.GameCode).ThenBy(x => x.Title).ToListAsync();
var existingDraft = await database.ReportDrafts.AsNoTracking().Include(x => x.Sections)
    .SingleOrDefaultAsync(x => x.ReportDate == reportDate);
var report = existingDraft is null ? string.Empty : ComposeExistingDraft(existingDraft, reportDate);
var unknownEnabled = await database.Birthdays.CountAsync(x => (x.Month == 0 || x.Day == 0) && x.Enabled);
var conflictEnabled = await database.Birthdays.CountAsync(x => x.VerificationStatus == VerificationStatus.Conflict && x.Enabled);
var confirmedUnverified = await database.TimelineItems.CountAsync(x =>
    x.ReviewStatus == ReviewStatus.Confirmed &&
    (x.VerificationStatus == VerificationStatus.Unverified || x.VerificationStatus == VerificationStatus.Conflict));
var reportEligibleUnverified = items.Count(x =>
    ReportEligibility.CanInclude(x) &&
    (x.VerificationStatus == VerificationStatus.Unverified || x.VerificationStatus == VerificationStatus.Conflict));
var reviewActionCount = await database.ReviewActions.CountAsync();
var evidenceCount = await database.Evidence.CountAsync();
var confirmedWithEvidence = items.Count(x => x.Evidence.Count > 0);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
await writer.WriteLineAsync("# 日报条目反向追溯");
await writer.WriteLineAsync();
await writer.WriteLineAsync($"- 数据库：`{databasePath}`");
await writer.WriteLineAsync($"- 报告日期：`{reportDate:yyyy-MM-dd}`");
await writer.WriteLineAsync($"- 已确认 TimelineItem：`{items.Count}`");
await writer.WriteLineAsync($"- 报告字符数：`{report.Length}`");
await writer.WriteLineAsync($"- SQLite 安全门：`unknown_enabled={unknownEnabled}`、`conflict_enabled={conflictEnabled}`、`confirmed_unverified={confirmedUnverified}`、`report_eligible_unverified={reportEligibleUnverified}`");
await writer.WriteLineAsync("- 安全门说明：`report_eligible_unverified` 是 ReportEligibility gate invariant；`confirmed_unverified` 单独记录被日报过滤的确认候选，不代表其已进入报告正文。");
await writer.WriteLineAsync($"- 审计追溯计数：`review_actions={reviewActionCount}`、`evidence={evidenceCount}`、`confirmed_with_evidence={confirmedWithEvidence}`");
await writer.WriteLineAsync();
await writer.WriteLineAsync("## 追溯链");
await writer.WriteLineAsync();
await writer.WriteLineAsync("| TimelineItem | 游戏 | 审核状态 | Evidence | Source URL |");
await writer.WriteLineAsync("|---|---|---|---|---|");
foreach (var item in items)
{
    var evidence = item.Evidence.Count == 0 ? ["无"] : item.Evidence.Select(x => $"{x.SourceProvider}: {x.SourceType}").ToArray();
    var urls = item.Evidence.Count == 0 ? ["无"] : item.Evidence.Select(x => x.SourceUrl).Distinct(StringComparer.Ordinal).ToArray();
    await writer.WriteLineAsync($"| `{item.Id}` {Escape(item.Title)} | {Escape(item.GameCode)} | `{item.ReviewStatus}` | {Escape(string.Join("<br>", evidence))} | {Escape(string.Join("<br>", urls))} |");
}

await writer.WriteLineAsync();
await writer.WriteLineAsync("## 报告正文快照");
await writer.WriteLineAsync();
await writer.WriteLineAsync("```");
await writer.WriteLineAsync(report);
await writer.WriteLineAsync("```");
Console.WriteLine($"ConfirmedItems={items.Count}; Evidence={items.Sum(x => x.Evidence.Count)}; unknown_enabled={unknownEnabled}; conflict_enabled={conflictEnabled}; confirmed_unverified={confirmedUnverified}; report_eligible_unverified={reportEligibleUnverified}; review_actions={reviewActionCount}; evidence={evidenceCount}; confirmed_with_evidence={confirmedWithEvidence}; Output={Path.GetFullPath(outputPath)}");

static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

static string ComposeExistingDraft(ReportDraftEntity draft, DateOnly date)
{
    var builder = new StringBuilder();
    builder.AppendLine(draft.Title);
    builder.AppendLine();
    builder.AppendLine(DailyReportFormatter.DateLine(date));
    foreach (var section in draft.Sections.OrderBy(x => x.SortOrder).Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.Text)))
    {
        builder.AppendLine();
        builder.AppendLine(DailyReportFormatter.SectionTitle(section.Key));
        builder.AppendLine(section.Text);
        builder.AppendLine("——————————————————");
    }

    return builder.ToString().TrimEnd();
}
