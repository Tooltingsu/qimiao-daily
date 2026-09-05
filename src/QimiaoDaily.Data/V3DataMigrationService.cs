using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using System.Text.Json;

namespace QimiaoDaily.Data;

/// <summary>
/// Applies the one-way provenance correction required when moving from automatic game-data
/// collection to the V3 manual workflow. It never upgrades legacy business records to manual.
/// </summary>
public sealed class V3DataMigrationService(QimiaoDailyDbContext database)
{
    public async Task<V3DataMigrationResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var timelineItems = await database.TimelineItems.ToListAsync(cancellationToken);
        var legacyBusinessItems = 0;
        foreach (var item in timelineItems)
        {
            if (!IsLegacyBusinessItem(item)) continue;

            if (item.DataOrigin != DataOrigin.LegacyAuto || item.UserConfirmed)
            {
                item.SetDataProvenance(DataOrigin.LegacyAuto, false);
                legacyBusinessItems++;
            }
        }

        var birthdays = await database.Birthdays.ToListAsync(cancellationToken);
        var birthdaysInitialized = 0;
        foreach (var birthday in birthdays)
        {
            if (birthday.DataOrigin == DataOrigin.Imported && birthday.UserConfirmed && !string.IsNullOrWhiteSpace(birthday.OriginTrace)) continue;
            birthday.DataOrigin = DataOrigin.Imported;
            birthday.UserConfirmed = true;
            birthday.OriginTrace = BuildOriginTrace(birthday);
            birthdaysInitialized++;
        }

        var endgameRulesSeeded = await SeedEndgameRulesAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return new V3DataMigrationResult(legacyBusinessItems, birthdaysInitialized, endgameRulesSeeded);
    }

    private async Task<int> SeedEndgameRulesAsync(CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            ("GENSHIN_SPIRAL_ABYSS", "GENSHIN", "深境螺旋", "MONTHLY", "EXACT", "04:00"),
            ("GENSHIN_IMAGINARIUM_THEATER", "GENSHIN", "幻想真境剧诗", "MONTHLY", "EXACT", "04:00"),
            ("GENSHIN_STYGIAN_ONSLAUGHT", "GENSHIN", "幽境危战", "VERSION_STYGIAN", "EXACT", "10:00"),
            ("GENSHIN_FRENZIED_ONSLAUGHT", "GENSHIN", "幽境危战·纷乱爆发", "VERSION_FRENZIED", "EXACT", "10:00"),
            ("STARRAIL_MEMORY_OF_CHAOS", "STARRAIL", "混沌回忆", "INTERVAL", "EXACT", "04:00"),
            ("STARRAIL_APOCALYPTIC_SHADOW", "STARRAIL", "末日幻影", "INTERVAL", "EXACT", "04:00"),
            ("STARRAIL_PURE_FICTION", "STARRAIL", "虚构叙事", "INTERVAL", "EXACT", "04:00"),
            ("STARRAIL_SECTOR_ARBITRATION", "STARRAIL", "异相仲裁", "VERSION_BOUNDED", "EXACT", "04:00"),
            ("NTE_OUTER_REALM", "NTE", "轨外之境", "INTERVAL", "DATE_ONLY", "")
        };
        var existing = await database.EndgameRules.ToDictionaryAsync(x => x.RuleKey, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var added = 0;
        foreach (var seed in seeds)
        {
            if (existing.ContainsKey(seed.Item1)) continue;
            var intervalDays = seed.Item1 == "NTE_OUTER_REALM" ? 14 : seed.Item4 == "INTERVAL" ? 42 : 1;
            database.EndgameRules.Add(new EndgameRuleEntity
            {
                RuleKey = seed.Item1, Game = seed.Item2, Name = seed.Item3, RuleKind = seed.Item4,
                TimePrecision = seed.Item5, StartTime = string.IsNullOrEmpty(seed.Item6) ? null : TimeOnly.Parse(seed.Item6),
                ConfigurationJson = JsonSerializer.Serialize(new { intervalDays, timePrecision = seed.Item5, anchor = seed.Item1 == "NTE_OUTER_REALM" ? "2026-08-21" : string.Empty })
            });
            added++;
        }
        return added;
    }

    private static string BuildOriginTrace(BirthdayEntity birthday)
        => $"V3_INITIAL_IMPORT | Source={birthday.Source} | SourceUrl={birthday.SourceUrl} | Evidence={birthday.Evidence}";

    private static bool IsLegacyBusinessItem(TimelineItem item)
        => (string.Equals(item.ItemType, "EVENT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.ItemType, "GACHA", StringComparison.OrdinalIgnoreCase))
           && item.DataOrigin == DataOrigin.AutoCollected
           && !item.UserConfirmed;
}

public sealed record V3DataMigrationResult(int LegacyBusinessTimelineItems, int BirthdaysInitialized, int EndgameRulesSeeded);
