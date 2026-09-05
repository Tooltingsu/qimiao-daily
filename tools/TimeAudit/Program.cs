using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;
using System.Security.Cryptography;
using System.Text;

var databasePath = Environment.GetEnvironmentVariable("QIMIAO_TIME_AUDIT_DB")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QimiaoDaily", "data", "qimiao.db");
var outputPath = Environment.GetEnvironmentVariable("QIMIAO_TIME_AUDIT_OUTPUT")
    ?? Path.Combine(Environment.CurrentDirectory, "time-audit.csv");
var birthdayOutputPath = Environment.GetEnvironmentVariable("QIMIAO_BIRTHDAY_AUDIT_OUTPUT");

var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>()
    .UseSqlite($"Data Source={databasePath}")
    .Options;
await using var database = new QimiaoDailyDbContext(options);

var games = new[] { "GENSHIN", "STARRAIL", "NTE" };
var rows = await database.TimelineItems
    .AsNoTracking()
    .Include(item => item.Evidence)
    .Where(item => games.Contains(item.GameCode))
    .OrderBy(item => item.GameCode)
    .ThenBy(item => item.ItemType)
    .ThenBy(item => item.Title)
    .ToListAsync();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await using var writer = new StreamWriter(outputPath, false, new System.Text.UTF8Encoding(false));
await writer.WriteLineAsync("game,title,item_type,review_status,verification_status,source_time,start_at,end_at,start_precision,end_precision,start_source,end_source,start_expression,end_expression,start_evidence_key,end_evidence_key,evidence_count,stored_evidence,stored_evidence_sha256,source_urls,verdict");
foreach (var item in rows)
{
    var evidence = item.Evidence
        .Where(e => !string.IsNullOrWhiteSpace(e.SourceUrl) || !string.IsNullOrWhiteSpace(e.SourceText))
        .ToArray();
    var urls = string.Join(" | ", evidence.Select(e => e.SourceUrl).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
    var storedEvidence = evidence.Length > 0 && evidence.All(e => !string.IsNullOrWhiteSpace(e.SourceUrl) && !string.IsNullOrWhiteSpace(e.SourceText));
    var evidenceFingerprint = storedEvidence
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", evidence.OrderBy(e => e.SourceUrl, StringComparer.Ordinal).Select(e => $"{e.SourceUrl}\n{e.SourceText}")))))
        : string.Empty;
    var verdict = storedEvidence ? "STORED_EVIDENCE_AVAILABLE" : "REVIEW_REQUIRED";
    var values = new[]
    {
        item.GameCode, item.Title, item.ItemType, item.ReviewStatus.ToString(), item.VerificationStatus.ToString(), item.SourceTime ?? "",
        item.NormalizedTime?.ToString("O") ?? "", item.EndAt?.ToString("O") ?? "",
        item.StartTimePrecision.ToString(), item.EndTimePrecision.ToString(), item.StartTimeSource ?? "", item.EndTimeSource ?? "",
        item.StartExpression ?? "", item.EndExpression ?? "", item.StartTimeEvidenceKey ?? "", item.EndTimeEvidenceKey ?? "", evidence.Length.ToString(),
        storedEvidence.ToString(), evidenceFingerprint, urls, verdict
    };
    await writer.WriteLineAsync(string.Join(',', values.Select(Escape)));
}

Console.WriteLine($"Database={databasePath}");
Console.WriteLine($"Rows={rows.Count}");
Console.WriteLine($"Output={Path.GetFullPath(outputPath)}");

if (!string.IsNullOrWhiteSpace(birthdayOutputPath))
{
    var birthdayFranchises = new[] { "HI3", "NTE" };
    var birthdays = await database.Birthdays.AsNoTracking()
        .Where(item => birthdayFranchises.Contains(item.Franchise))
        .OrderBy(item => item.Franchise)
        .ThenBy(item => item.Character)
        .ToListAsync();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(birthdayOutputPath))!);
    await using var birthdayWriter = new StreamWriter(birthdayOutputPath, false, new System.Text.UTF8Encoding(false));
    await birthdayWriter.WriteLineAsync("franchise,character,canonical_name,month,day,source,source_url,evidence,verification_status,enabled");
    foreach (var item in birthdays)
    {
        var values = new[] { item.Franchise, item.Character, item.CanonicalCharacterNameZhCn ?? "", item.Month.ToString(), item.Day.ToString(), item.Source, item.SourceUrl, item.Evidence, item.VerificationStatus.ToString(), item.Enabled.ToString() };
        await birthdayWriter.WriteLineAsync(string.Join(',', values.Select(Escape)));
    }
    Console.WriteLine($"BirthdayRows={birthdays.Count}");
    Console.WriteLine($"BirthdayOutput={Path.GetFullPath(birthdayOutputPath)}");
}

static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
