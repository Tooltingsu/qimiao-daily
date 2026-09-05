using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class DailyReportService(QimiaoDailyDbContext database)
{
    public static readonly string[] SectionOrder = ["calendar", "games", "bgi", "artwork"];

    public async Task<ReportDraftEntity> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var draft = await database.ReportDrafts
            .Include(x => x.Sections)
            .SingleOrDefaultAsync(x => x.ReportDate == date, cancellationToken);
        if (draft is not null)
        {
            return draft;
        }

        draft = new ReportDraftEntity
        {
            ReportDate = date,
            Title = "\u7eee\u55b5\u65e5\u62a5 " + date.ToString("yyMMdd"),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        for (var index = 0; index < SectionOrder.Length; index++)
        {
            draft.Sections.Add(new ReportSectionEntity
            {
                ReportDraftId = draft.Id,
                Key = SectionOrder[index],
                SortOrder = index,
                Text = string.Empty
            });
        }

        database.ReportDrafts.Add(draft);
        await database.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<bool> RebuildSectionAsync(DateOnly date, string key, string generatedText, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var section = draft.Sections.SingleOrDefault(x => x.Key == key);
        if (section is null)
        {
            section = new ReportSectionEntity { ReportDraftId = draft.Id, Key = key, SortOrder = draft.Sections.Count, Text = string.Empty };
            draft.Sections.Add(section);
        }
        if (section.IsDeleted) return false;
        if (section.Dirty || section.ManualOverride)
        {
            return false;
        }

        section.Text = generatedText;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateManualSectionAsync(DateOnly date, string key, string text, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var section = draft.Sections.Single(x => x.Key == key);
        section.Text = text;
        section.IsDeleted = false;
        section.Dirty = true;
        section.ManualOverride = true;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAutomaticSectionAsync(DateOnly date, string key, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var section = draft.Sections.Single(x => x.Key == key);
        section.Dirty = false;
        section.ManualOverride = false;
        section.IsDeleted = false;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ComposeAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var sections = draft.Sections
            .OrderBy(x => x.SortOrder)
            .Where(x => !x.IsDeleted && (!string.IsNullOrWhiteSpace(x.Text) || x.Key == "games"))
            .Select(x => (x.Key, x.Text))
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine(draft.Title);
        builder.AppendLine();
        builder.AppendLine(DailyReportFormatter.DateLine(date));
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            builder.AppendLine();
            if (section.Key is "games" or "artwork")
                builder.AppendLine(DailyReportFormatter.SectionTitle(section.Key));
            builder.AppendLine(section.Text);
            if (index < sections.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine(DailyReportFormatter.SectionSeparator);
            }
        }
        return builder.ToString().TrimEnd();
    }

    public async Task ExportAsync(DateOnly date, string path, bool markdown, CancellationToken cancellationToken = default)
    {
        var text = await ComposeAsync(date, cancellationToken);
        if (markdown)
        {
            text = "# " + text;
        }

        await File.WriteAllTextAsync(path, text, Encoding.UTF8, cancellationToken);
    }

    public async Task BuildAutomaticSectionsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        foreach (var key in SectionOrder)
            await BuildAutomaticSectionAsync(date, key, cancellationToken);
    }

    public async Task<bool> BuildAutomaticSectionAsync(DateOnly date, string key, CancellationToken cancellationToken = default)
    {
        var generatedText = await GenerateAutomaticSectionTextAsync(date, key, cancellationToken);
        return await RebuildSectionAsync(date, key, generatedText, cancellationToken);
    }

    private async Task<string> GenerateAutomaticSectionTextAsync(DateOnly date, string key, CancellationToken cancellationToken)
    {
        return key switch
        {
            "calendar" => DailyReportFormatter.FormatCalendar(await new CalendarService(database).ForDateAsync(date, cancellationToken)),
            "games" => await GenerateV3GamesAsync(date, cancellationToken),
            "bgi" => DailyReportFormatter.FormatBgi(await database.GitCommitRecords
                .AsNoTracking()
                .Where(x => x.SelectedForReport)
                .ToListAsync(cancellationToken)),
            "artwork" => DailyReportFormatter.FormatArtworks(await database.Artworks
                .AsNoTracking()
                .Where(x => x.ReviewStatus == ReviewStatus.Confirmed && x.SelectedForReport)
                .ToListAsync(cancellationToken)),
            _ => throw new ArgumentException("未知的日报 Section。", nameof(key))
        };
    }

    private async Task<string> GenerateV3GamesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var events = await database.ManualEvents.AsNoTracking().ToListAsync(cancellationToken);
        var banners = await database.Banners.AsNoTracking().Include(x => x.Characters).ToListAsync(cancellationToken);
        var rules = await database.EndgameRules.AsNoTracking().Where(x => x.Enabled).ToListAsync(cancellationToken);
        var ruleById = rules.ToDictionary(x => x.Id);
        var occurrences = (await database.EndgameOccurrences.AsNoTracking().ToListAsync(cancellationToken))
            .Where(x => ruleById.TryGetValue(x.RuleId, out _))
            .Select(x =>
            {
                var rule = ruleById[x.RuleId];
                return new EndgameReportOccurrence(rule.Game, rule.Name, x.StartAt, IsDateOnlyRule(rule));
            })
            .ToList();
        var automaticTimeline = (await database.TimelineItems
                .Include(x => x.Evidence)
                .ToListAsync(cancellationToken))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CanonicalIdentity) ? x.Id.ToString() : x.CanonicalIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(item => item.FetchedAt).First())
            .Where(x => x.DataOrigin != DataOrigin.LegacyAuto)
            .Where(ReportEligibility.CanInclude)
            .ToList();
        return DailyReportFormatter.FormatV3Games(events, banners, occurrences, automaticTimeline, date);
    }

    private static bool IsDateOnlyRule(EndgameRuleEntity rule)
    {
        try
        {
            using var document = JsonDocument.Parse(rule.ConfigurationJson);
            var root = document.RootElement;
            var hasPrecision = root.TryGetProperty("timePrecision", out var value)
                || root.TryGetProperty("TimePrecision", out value);
            return hasPrecision && string.Equals(value.GetString(), "DATE_ONLY", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return string.Equals(rule.RuleKind, "DATE_ONLY", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task DeleteSectionAsync(DateOnly date, string key, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var section = draft.Sections.Single(x => x.Key == key);
        section.IsDeleted = true;
        section.Text = string.Empty;
        section.Dirty = true;
        section.ManualOverride = true;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveSectionAsync(DateOnly date, string key, int direction, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateAsync(date, cancellationToken);
        var ordered = draft.Sections.Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder).ToList();
        var index = ordered.FindIndex(x => x.Key == key);
        var target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= ordered.Count) return;
        (ordered[index].SortOrder, ordered[target].SortOrder) = (ordered[target].SortOrder, ordered[index].SortOrder);
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }
}
