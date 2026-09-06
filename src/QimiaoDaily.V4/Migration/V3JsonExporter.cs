using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;
using QimiaoDaily.Services;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Migration;

public sealed class V3JsonExporter(V4Repository repository)
{
    public async Task<IReadOnlyDictionary<string, int>> ExportAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("V3 SQLite database was not found.", databasePath);
        var options = new DbContextOptionsBuilder<QimiaoDailyDbContext>().UseSqlite($"Data Source={databasePath};Mode=ReadOnly").Options;
        await using var database = new QimiaoDailyDbContext(options);

        var activities = (await database.ManualEvents.AsNoTracking().Where(x => !x.Archived).ToListAsync(cancellationToken))
            .Select(x => new ManualEventRecord(x.Id.ToString(), x.Game, x.Name, x.StartAt, x.EndAt, x.Notes, x.UserConfirmed)).ToList();
        var banners = (await database.Banners.AsNoTracking().Include(x => x.Characters).Where(x => !x.Archived).ToListAsync(cancellationToken))
            .Select(x => new BannerRecord(x.Id.ToString(), x.Game, x.Name, x.CustomType ?? x.Type, x.StartAt, x.EndAt,
                x.Characters.OrderBy(c => c.SortOrder).Select(c => c.Name).ToList(), x.Notes, x.UserConfirmed)).ToList();
        var versions = (await database.GameVersions.AsNoTracking().Where(x => !x.Archived).ToListAsync(cancellationToken))
            .Select(x => new VersionRecord(x.Id.ToString(), x.Game, x.VersionNumber, x.VersionName, x.StartAt, x.EndAt, x.Notes, x.UserConfirmed)).ToList();
        var birthdays = (await database.Birthdays.AsNoTracking().ToListAsync(cancellationToken))
            .Select(x => new QimiaoDaily.V4.Core.BirthdayRecord(x.Id.ToString(), x.Character, x.Franchise, x.Month, x.Day, x.Enabled, x.Source, x.SourceUrl, x.Evidence)).ToList();
        var anniversaries = (await database.Anniversaries.AsNoTracking().ToListAsync(cancellationToken))
            .Select(x => new QimiaoDaily.V4.Core.AnniversaryRecord(x.Id.ToString(), x.Title, x.StartedOn, x.Enabled, x.Notes)).ToList();
        var calendarEvents = (await database.CalendarEvents.AsNoTracking().ToListAsync(cancellationToken))
            .Select(x => new ManualCalendarEventRecord(x.Id.ToString(), x.EventDate, x.Kind, x.Title, x.Detail ?? string.Empty,
                x.Source, x.SourceUrl ?? string.Empty, x.Enabled)).ToList();

        var anchors = await database.EndgameAnchors.AsNoTracking().ToListAsync(cancellationToken);
        var ruleEntities = await database.EndgameRules.AsNoTracking().ToListAsync(cancellationToken);
        var rules = new List<EndgameRuleRecord>();
        var overrides = new List<EndgameOverrideRecord>();
        foreach (var entity in ruleEntities)
        {
            var defaults = EndgameScheduleRules.All.SingleOrDefault(x => x.RuleId == entity.RuleKey);
            EndgameSchedulePersistenceConfiguration? configuration = null;
            try { configuration = JsonSerializer.Deserialize<EndgameSchedulePersistenceConfiguration>(entity.ConfigurationJson, V4Repository.JsonOptions); } catch { }
            var anchor = anchors.Where(x => x.RuleId == entity.Id).OrderByDescending(x => x.AnchorDate).FirstOrDefault()?.AnchorDate ?? defaults?.AnchorDate ?? new DateOnly(2026, 1, 1);
            var interval = configuration?.IntervalDays is > 0 ? configuration.IntervalDays : defaults?.IntervalDays ?? 1;
            var startTime = entity.TimePrecision.Equals("DATE_ONLY", StringComparison.OrdinalIgnoreCase) ? null : entity.StartTime ?? defaults?.StartTime;
            rules.Add(new(entity.RuleKey, entity.Game, entity.Name, entity.RuleKind, anchor, interval, entity.TimePrecision, startTime, entity.Enabled));
            overrides.AddRange((configuration?.Overrides ?? []).Select(x => new EndgameOverrideRecord(entity.RuleKey, x.ScheduledStart, x.StartsOn, x.StartTime, x.EndsOn, x.EndTime, x.Suppressed, x.Notes ?? string.Empty)));
        }
        foreach (var defaults in EndgameScheduleRules.All.Where(x => rules.All(r => r.RuleId != x.RuleId)))
            rules.Add(new(defaults.RuleId, defaults.GameCode, defaults.DisplayName, defaults.RuleKind, defaults.AnchorDate, defaults.IntervalDays,
                defaults.Precision == EndgameTimePrecision.DateOnly ? "DATE_ONLY" : "EXACT", defaults.StartTime, true));

        var artworks = (await database.Artworks.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new ArtworkRecord(
            x.Platform, x.ArtworkId, x.CharacterName, x.FranchiseName, x.Title, x.Author, x.SourceUrl,
            Uri.TryCreate(x.ThumbnailUrl, UriKind.Absolute, out var thumbnail) && thumbnail.Scheme == Uri.UriSchemeHttps ? x.ThumbnailUrl : string.Empty,
            x.ReviewStatus.ToString().ToUpperInvariant(), x.SelectedForReport, x.PublishedAt, x.FetchedAt)).ToList();
        // Legacy V3 only had a boolean selection flag. Preserve its existing
        // selected confirmed rows as a deterministic initial FIFO queue;
        // subsequent ordering is explicit user-owned V4 data.
        var artworkQueue = artworks
            .Where(x => x.SelectedForReport && x.ReviewStatus.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.FetchedAt).ThenBy(x => x.ArtworkId, StringComparer.Ordinal)
            .Select((x, index) => new ArtworkQueueEntry(x.Platform, x.ArtworkId, index + 1)).ToList();
        var videos = (await database.TimelineItems.AsNoTracking().Include(x => x.Evidence)
            .Where(x => x.ItemType == "VIDEO" || x.ItemType == "PREVIEW_NOTICE" || x.ItemType == "PREVIEW_LIVE").ToListAsync(cancellationToken))
            .Select(x => new VideoRecord(x.Id.ToString(), x.GameCode, x.ItemType, x.Title, x.Evidence.FirstOrDefault()?.SourceUrl ?? string.Empty,
                x.NormalizedTime, x.ReviewStatus.ToString().ToUpperInvariant(), x.FetchedAt)).ToList();
        var commits = (await database.GitCommitRecords.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new BgiCommitRecord(
            x.Repository, x.Sha, x.Subject, x.Url, x.CommitterDate ?? x.AuthorDate, x.FetchedAt)).ToList();

        repository.Write(activities, "data", "activities.json");
        repository.Write(banners, "data", "banners.json");
        repository.Write(versions, "data", "versions.json");
        repository.Write(rules.OrderBy(x => x.RuleId).ToList(), "data", "endgame-rules.json");
        repository.Write(overrides.OrderBy(x => x.RuleId).ThenBy(x => x.ScheduledStart).ToList(), "data", "endgame-overrides.json");
        repository.Write(birthdays.OrderBy(x => x.Franchise).ThenBy(x => x.Month).ThenBy(x => x.Day).ThenBy(x => x.Character).ToList(), "data", "birthdays.json");
        repository.Write(anniversaries.OrderBy(x => x.StartedOn).ToList(), "data", "anniversaries.json");
        repository.Write(calendarEvents.OrderBy(x => x.EventDate).ThenBy(x => x.Kind).ThenBy(x => x.Title).ToList(), "data", "calendar-events.json");
        repository.Write(artworkQueue, "data", "artwork-queue.json");
        repository.Write(artworks, "collected", "artwork.json");
        repository.Write(videos, "collected", "videos.json");
        repository.Write(commits.Where(x => !x.Repository.Contains("scripts", StringComparison.OrdinalIgnoreCase)).ToList(), "collected", "bgi-main.json");
        repository.Write(commits.Where(x => x.Repository.Contains("scripts", StringComparison.OrdinalIgnoreCase)).ToList(), "collected", "bgi-scripts.json");
        repository.Write(new List<ProviderStatusRecord>(), "collected", "provider-status.json");

        return new Dictionary<string, int>
        {
            ["activities"] = activities.Count, ["banners"] = banners.Count, ["versions"] = versions.Count,
            ["endgameRules"] = rules.Count, ["endgameOverrides"] = overrides.Count, ["birthdays"] = birthdays.Count,
            ["anniversaries"] = anniversaries.Count, ["calendarEvents"] = calendarEvents.Count,
            ["artworks"] = artworks.Count, ["artworkQueue"] = artworkQueue.Count, ["videos"] = videos.Count, ["bgiCommits"] = commits.Count
        };
    }
}
