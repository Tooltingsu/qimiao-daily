using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>
/// Materializes only announcement-backed endgame windows. It deliberately does not
/// extrapolate future dates when an official announcement has not supplied them.
/// </summary>
public sealed class EndgameCycleService(QimiaoDailyDbContext database)
{
    public async Task<bool> UpsertFromCandidateAsync(GameCandidate candidate, Guid timelineItemId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(candidate.ItemType, "ENDGAME", StringComparison.OrdinalIgnoreCase) || candidate.NormalizedTime is null)
            return false;

        var evidence = candidate.Evidence.FirstOrDefault();
        if (evidence is null || string.IsNullOrWhiteSpace(evidence.SourceUrl))
            return false;

        var canonical = Canonicalize(candidate.Title);
        var rule = database.EndgameCycleRules.Local.SingleOrDefault(
                       x => x.GameCode == candidate.GameCode && x.CanonicalName == canonical && x.RuleVersion == "official-announcement-v1")
                   ?? await database.EndgameCycleRules.SingleOrDefaultAsync(
                       x => x.GameCode == candidate.GameCode && x.CanonicalName == canonical && x.RuleVersion == "official-announcement-v1",
                       cancellationToken);
        if (rule is null)
        {
            rule = new EndgameCycleRuleEntity
            {
                GameCode = candidate.GameCode,
                CanonicalName = canonical,
                DisplayName = candidate.Title,
                RecurrenceKind = "ANNOUNCEMENT_BACKED",
                RuleVersion = "official-announcement-v1",
                SourceUrl = evidence.SourceUrl,
                Evidence = evidence.SourceText,
                AnchorStart = candidate.NormalizedTime,
                VerificationStatus = VerificationStatus.VerifiedOfficial,
                Enabled = true
            };
            database.EndgameCycleRules.Add(rule);
        }

        var instance = await database.EndgameCycleInstances.SingleOrDefaultAsync(x => x.TimelineItemId == timelineItemId, cancellationToken)
                       ?? await database.EndgameCycleInstances.SingleOrDefaultAsync(
                           x => x.GameCode == candidate.GameCode && x.CanonicalName == canonical && x.StartAt == candidate.NormalizedTime.Value,
                           cancellationToken);
        if (instance is null)
        {
            database.EndgameCycleInstances.Add(new EndgameCycleInstanceEntity
            {
                RuleId = rule.Id,
                GameCode = candidate.GameCode,
                CanonicalName = canonical,
                DisplayName = candidate.Title,
                StartAt = candidate.NormalizedTime.Value,
                EndAt = candidate.EndAt,
                RuleVersion = rule.RuleVersion,
                TimelineItemId = timelineItemId,
                SourceUrl = evidence.SourceUrl,
                VerificationStatus = VerificationStatus.VerifiedOfficial,
                ReviewStatus = ReviewStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            });
            return true;
        }

        // Keep the original instance as an auditable snapshot; a changed announcement
        // is represented by a new TimelineItem and therefore a new instance.
        return false;
    }

    public static string Canonicalize(string title)
    {
        if (title.Contains("深境螺旋", StringComparison.Ordinal)) return "GENSHIN_SPIRAL_ABYSS";
        if (title.Contains("幻想真境剧诗", StringComparison.Ordinal)) return "GENSHIN_IMAGINARIUM_THEATER";
        if (title.Contains("幽境危战", StringComparison.Ordinal)) return "GENSHIN_STYGIAN_ONSLAUGHT";
        if (title.Contains("混沌回忆", StringComparison.Ordinal)) return "STARRAIL_MEMORY_OF_CHAOS";
        if (title.Contains("虚构叙事", StringComparison.Ordinal)) return "STARRAIL_PURE_FICTION";
        if (title.Contains("末日幻影", StringComparison.Ordinal)) return "STARRAIL_APOCALYPTIC_SHADOW";
        if (title.Contains("异相仲裁", StringComparison.Ordinal)) return "STARRAIL_SECTOR_ARBITRATION";
        return "UNCLASSIFIED_ENDGAME";
    }
}
