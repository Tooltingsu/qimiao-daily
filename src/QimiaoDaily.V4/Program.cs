using QimiaoDaily.V4.Collectors;
using QimiaoDaily.V4.Core;
using QimiaoDaily.V4.Generator;
using QimiaoDaily.V4.Migration;
using QimiaoDaily.V4.Publishing;
using QimiaoDaily.V4.Validator;
using QimiaoDaily.V4.Web;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var root = Option("--root") ?? Directory.GetCurrentDirectory();
var repository = new V4Repository(root);
var now = DateTimeOffset.UtcNow;
var date = DateOnly.TryParse(Option("--date"), out var parsedDate)
    ? parsedDate
    : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")).DateTime);

try
{
    switch (command)
    {
        case "export-v3":
            var database = Option("--db") ?? throw new ArgumentException("--db is required.");
            Print(await new V3JsonExporter(repository).ExportAsync(database));
            break;
        case "validate":
            var validation = new V4Validator(repository).ValidateAll();
            Print(validation);
            Environment.ExitCode = validation.IsValid ? 0 : 2;
            break;
        case "calculate":
            EnsureValid(repository);
            var calculator = new V4Calculator(repository);
            var endgame = calculator.CalculateEndgame(date);
            var calendar = calculator.CalculateCalendar(date.Year);
            Print(new { endgame = endgame.Count, calendar = calendar.Count });
            break;
        case "collect-bgi":
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                Print(new { collected = await new V4BgiCollector(repository, client).CollectAsync(date, now) });
            break;
        case "collect":
            EnsureValid(repository);
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                var count = await new V4BgiCollector(repository, client).CollectAsync(date, now);
                await new V4MediaCollector(repository, client).CollectAsync(date, now);
                Print(new { collected = count, providers = repository.Read<List<ProviderStatusRecord>>("collected", "provider-status.json") });
            }
            break;
        case "generate":
            EnsureValid(repository);
            Print(new V4ReportGenerator(repository).Generate(date, Option("--source-commit") ?? "LOCAL_POC", now));
            break;
        case "lock":
            EnsureValid(repository);
            Print(new V4PublishService(repository).Lock(date, (Option("--mode") ?? "manual") == "manual", now));
            break;
        case "replace-lock":
            EnsureValid(repository);
            if (!int.TryParse(Option("--revision"), out var requestedRevision))
                throw new ArgumentException("replace-lock requires --revision.");
            Print(new V4PublishService(repository).ReplaceUnpublishedLock(date, requestedRevision, now, Option("--reason") ?? string.Empty));
            break;
        case "confirm-publish":
            EnsureValid(repository);
            if (!int.TryParse(Option("--revision"), out var confirmedRevision))
                throw new ArgumentException("confirm-publish requires --revision.");
            Print(new V4PublishService(repository).ConfirmManualVisibility(date, confirmedRevision, now, Option("--reason") ?? string.Empty));
            break;
        case "publish":
            EnsureDryRun();
            EnsureValid(repository);
            if (Option("--simulate-deadline") == "true")
            {
                now = ShanghaiClock.At(date, TimeOnly.Parse(repository.Read<V4Settings>("data", "settings.json").PublishTime)).AddMinutes(1);
            }
            var priorLog = repository.ReadOr(new PublishLog { Date = date }, "publish-log", date + ".json");
            if (priorLog.Attempts.Any(x => x.Status is "PUBLISHED" or "DRY_RUN_SUCCEEDED"))
            {
                Print(new { status = "SKIPPED_ALREADY_PUBLISHED", date });
                break;
            }
            if (bool.TryParse(Option("--watchdog") ?? "false", out var watchdog) && watchdog)
            {
                var window = new PublishWindowGuard(repository.Read<V4Settings>("data", "settings.json")).Evaluate(now);
                if (!window.ShouldPublish)
                {
                    Print(new { skipped = true, window.Reason, window.ReportDate });
                    break;
                }
                date = window.ReportDate;
                var existingLog = repository.ReadOr(new PublishLog { Date = date }, "publish-log", date + ".json");
                if (existingLog.Attempts.Any(x => x.Status is "PUBLISHED" or "DRY_RUN_SUCCEEDED"))
                {
                    Print(new { skipped = true, reason = "Already published for this date.", reportDate = date });
                    break;
                }
            }
            try
            {
                Print(new V4PublishService(repository).PublishDryRun(date, Option("--workflow-run") ?? "LOCAL_POC", now));
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("Idempotency guard:", StringComparison.Ordinal))
            {
                // A racing watchdog invocation must be a successful no-op, not
                // a failed workflow.  PublishDryRun retains the same guard as
                // the final defense in case a log appeared after the precheck.
                Print(new { status = "SKIPPED_ALREADY_PUBLISHED", date, reason = error.Message });
            }
            break;
        case "republish":
            EnsureDryRun();
            if (Option("--force") != "true" || string.IsNullOrWhiteSpace(Option("--reason")))
                throw new ArgumentException("Republish requires --force true and --reason.");
            EnsureValid(repository);
            var service = new V4PublishService(repository);
            var revision = service.PrepareRepublication(date, Option("--source-commit") ?? "LOCAL_POC", now, Option("--reason") ?? "manual correction");
            service.Lock(date, true, now);
            var attempt = service.PublishDryRun(date, Option("--workflow-run") ?? "LOCAL_POC_REPUBLISH", now, force: true, Option("--reason"));
            Print(new { revision = revision.Revision, attempt.Status });
            break;
        case "build-pages":
            Print(new V4PagesBuilder(repository).Build(date));
            break;
        default:
            Console.WriteLine("QimiaoDaily V4 POC commands: export-v3, validate, calculate, collect-bgi, generate, lock, replace-lock, confirm-publish, publish, republish, build-pages");
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

string? Option(string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

void EnsureDryRun()
{
    if (!bool.TryParse(Option("--dry-run") ?? "true", out var dryRun) || !dryRun)
        throw new InvalidOperationException("BLOCKED_BY_USER: production QQ publishing is disabled during V4-B.");
}

static void EnsureValid(V4Repository repo)
{
    var validation = new V4Validator(repo).ValidateAll();
    if (!validation.IsValid) throw new InvalidDataException("V4 manual data validation failed: " + string.Join(" | ", validation.Issues.Select(x => $"{x.File} {x.Path}: {x.Message}")));
}

static void Print<T>(T value) => Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(value, V4Repository.JsonOptions));
