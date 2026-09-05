using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class BirthdayRefreshService(QimiaoDailyDbContext database, HoYoWikiBirthdayProvider provider, ThirdPartyBirthdayProvider? thirdPartyProvider = null)
{
    public async Task<BirthdayCoverageSnapshot> GetCoverageAsync(string franchise, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(franchise)) throw new ArgumentException("Franchise is required.", nameof(franchise));
        var rows = await database.Birthdays.AsNoTracking().Where(x => x.Franchise == franchise).ToListAsync(cancellationToken);
        var verifiedDates = rows.Count(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31 && x.VerificationStatus is VerificationStatus.VerifiedOfficial or VerificationStatus.VerifiedMultiSource);
        return new BirthdayCoverageSnapshot(franchise, rows.Count, verifiedDates, rows.Count - verifiedDates, rows.Count(x => x.Enabled));
    }

    public async Task<IReadOnlyList<BirthdayCoverageResult>> GetCoverageReportAsync(IEnumerable<string> franchises, CancellationToken cancellationToken = default)
    {
        var requested = franchises?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? throw new ArgumentNullException(nameof(franchises));
        var rows = await database.Birthdays.AsNoTracking().ToListAsync(cancellationToken);
        return requested.Select(franchise =>
        {
            var scoped = rows.Where(x => string.Equals(x.Franchise, franchise, StringComparison.OrdinalIgnoreCase)).ToArray();
            var known = scoped.Count(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31);
            var pending = scoped.Count(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31 && !x.Enabled);
            return new BirthdayCoverageResult(franchise, scoped.Length, known, scoped.Length - known, pending);
        }).ToArray();
    }

    public async Task<bool> RefreshAsync(int entryPageId, string franchise = "GENSHIN", CancellationToken cancellationToken = default)
    {
        return await RefreshManyAsync([new BirthdaySourceRequest(entryPageId, franchise)], cancellationToken) > 0;
    }

    public async Task<int> RefreshManyAsync(IEnumerable<BirthdaySourceRequest> sources, CancellationToken cancellationToken = default)
    {
        var result = await RefreshManyResilientAsync(sources, cancellationToken);
        if (result.Failed > 0)
            throw new InvalidOperationException(string.Join(" | ", result.Failures));
        return result.Changed;
    }

    public async Task<BirthdayRefreshResult> RefreshManyResilientAsync(IEnumerable<BirthdaySourceRequest> sources, CancellationToken cancellationToken = default)
    {
        var attempted = 0;
        var changed = 0;
        var verifiedDates = 0;
        var unknown = 0;
        var failures = new List<string>();
        foreach (var source in sources)
        {
            if (source.EntryPageId <= 0 || string.IsNullOrWhiteSpace(source.Franchise))
                throw new ArgumentException("Birthday source entries require a positive page id and franchise.");
            attempted++;
            try
            {
                var candidate = await provider.CollectAsync(source.EntryPageId, source.Franchise, cancellationToken);
                if (await UpsertAsync(candidate, cancellationToken)) changed++;
                if (candidate.IsUnknown) unknown++; else verifiedDates++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{source.Franchise}:{source.EntryPageId} {ex.Message}");
            }
        }

        return new BirthdayRefreshResult(attempted, changed, verifiedDates, unknown, failures.Count, failures);
    }

    public async Task<int> RefreshCandidatesAsync(IEnumerable<OfficialBirthdayCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var changed = 0;
        var franchiseSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            franchiseSet.Add(candidate.Franchise);
            var normalized = candidate with { Character = BirthdayCharacterNameMap.Resolve(candidate.Character) };
            if (await UpsertAsync(normalized, cancellationToken)) changed++;
        }
        await NormalizeCanonicalNamesAsync(franchiseSet, cancellationToken);
        return changed;
    }

    public async Task<int> RefreshMergedCandidatesAsync(IEnumerable<MergedBirthdayCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var changed = 0;
        var franchiseSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawCandidate in candidates)
        {
            var candidate = rawCandidate with { CanonicalCharacterNameZhCn = BirthdayCharacterNameMap.Resolve(rawCandidate.CanonicalCharacterNameZhCn) };
            franchiseSet.Add(candidate.Franchise);
            var sourceNames = candidate.Sources.Select(x => x.Character).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var scopedRecords = await database.Birthdays.Where(x => x.Franchise == candidate.Franchise).ToListAsync(cancellationToken);
            var record = scopedRecords.FirstOrDefault(x =>
                string.Equals(x.CanonicalCharacterNameZhCn, candidate.CanonicalCharacterNameZhCn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Character, candidate.CanonicalCharacterNameZhCn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(BirthdayCharacterNameMap.Resolve(x.CanonicalCharacterNameZhCn), candidate.CanonicalCharacterNameZhCn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(BirthdayCharacterNameMap.Resolve(x.Character), candidate.CanonicalCharacterNameZhCn, StringComparison.OrdinalIgnoreCase) ||
                sourceNames.Any(name => string.Equals(x.Character, name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(x.Aliases) && x.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.OrdinalIgnoreCase))));
            var unknown = candidate.Month is < 1 or > 12 || candidate.Day is < 1 or > 31;
            var sourceTier = candidate.VerificationStatus == VerificationStatus.VerifiedMultiSource ? "multi-source" : "third-party";
            var aliases = string.Join(", ", candidate.Sources.Select(x => x.Character).Distinct(StringComparer.OrdinalIgnoreCase));
            var urls = string.Join("\n", candidate.Sources.Select(x => x.SourceUrl).Distinct(StringComparer.OrdinalIgnoreCase));
            if (record is null)
            {
                database.Birthdays.Add(new BirthdayEntity
                {
                    Character = candidate.CanonicalCharacterNameZhCn,
                    CanonicalCharacterNameZhCn = candidate.CanonicalCharacterNameZhCn,
                    Aliases = aliases,
                    Franchise = candidate.Franchise,
                    Month = unknown ? 0 : candidate.Month,
                    Day = unknown ? 0 : candidate.Day,
                    Source = string.Join(", ", candidate.Sources.Select(x => x.Provider).Distinct(StringComparer.OrdinalIgnoreCase)),
                    SourceTier = sourceTier,
                    SourceUrl = urls,
                    Evidence = candidate.Evidence,
                    VerificationStatus = unknown ? VerificationStatus.Unverified : candidate.VerificationStatus,
                    VerifiedAt = DateTimeOffset.UtcNow,
                    Enabled = !unknown
                });
                changed++;
            }
            else
            {
                var nextStatus = unknown ? VerificationStatus.Unverified : candidate.VerificationStatus;
                var nextSource = string.Join(", ", candidate.Sources.Select(x => x.Provider).Distinct(StringComparer.OrdinalIgnoreCase));
                var isChanged = record.Character != candidate.CanonicalCharacterNameZhCn || record.Month != (unknown ? 0 : candidate.Month) || record.Day != (unknown ? 0 : candidate.Day) ||
                    record.SourceUrl != urls || record.Evidence != candidate.Evidence || record.VerificationStatus != nextStatus || record.Aliases != aliases;
                record.Character = candidate.CanonicalCharacterNameZhCn; record.CanonicalCharacterNameZhCn = candidate.CanonicalCharacterNameZhCn; record.Aliases = aliases;
                record.Month = unknown ? 0 : candidate.Month; record.Day = unknown ? 0 : candidate.Day; record.Source = nextSource; record.SourceTier = sourceTier; record.SourceUrl = urls; record.Evidence = candidate.Evidence; record.VerificationStatus = nextStatus; record.VerifiedAt = DateTimeOffset.UtcNow;
                if (unknown)
                    record.Enabled = false;
                if (isChanged) changed++;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        await NormalizeCanonicalNamesAsync(franchiseSet, cancellationToken);
        return changed;
    }

    public async Task<IReadOnlyList<BirthdayCoverageResult>> RefreshAllGamesAsync(
        IEnumerable<BirthdaySourceRequest> officialSources,
        IEnumerable<OfficialBirthdayCandidate>? rosterCandidates = null,
        IEnumerable<ThirdPartyBirthdaySourceRequest>? thirdPartySources = null,
        CancellationToken cancellationToken = default)
    {
        await RefreshManyResilientAsync(officialSources, cancellationToken);
        if (rosterCandidates is not null) await RefreshCandidatesAsync(rosterCandidates, cancellationToken);
        if (thirdPartyProvider is not null && thirdPartySources is not null)
        {
            foreach (var source in thirdPartySources)
            {
                var merged = await thirdPartyProvider.CollectAsync(new Uri(source.Url), source.Franchise, source.Provider, cancellationToken);
                await RefreshMergedCandidatesAsync(merged, cancellationToken);
            }
        }
        return await GetCoverageReportAsync(["GENSHIN", "HI3", "NTE"], cancellationToken);
    }

    public async Task NormalizeCanonicalNamesAsync(IEnumerable<string> franchises, CancellationToken cancellationToken = default)
    {
        var requested = franchises.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requested.Length == 0) return;
        var rows = await database.Birthdays.Where(x => requested.Contains(x.Franchise)).ToListAsync(cancellationToken);
        foreach (var group in rows.GroupBy(x => (x.Franchise, Canonical: BirthdayCharacterNameMap.Resolve(x.CanonicalCharacterNameZhCn))))
        {
            var ordered = group.OrderByDescending(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31)
                .ThenByDescending(x => x.VerificationStatus is VerificationStatus.VerifiedOfficial or VerificationStatus.VerifiedMultiSource)
                .ThenByDescending(x => x.VerifiedAt)
                .ToArray();
            var winner = ordered[0];
            var aliases = ordered.SelectMany(x => new[] { x.Character, x.CanonicalCharacterNameZhCn }.Concat((x.Aliases ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
                .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, group.Key.Canonical, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var duplicate in ordered.Skip(1))
            {
                winner.Source = string.Join(", ", new[] { winner.Source, duplicate.Source }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
                winner.SourceUrl = string.Join("\n", new[] { winner.SourceUrl, duplicate.SourceUrl }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
                winner.Evidence = string.Join("\n", new[] { winner.Evidence, duplicate.Evidence }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
                winner.Enabled &= duplicate.Enabled;
                database.Birthdays.Remove(duplicate);
            }
            winner.Character = group.Key.Canonical;
            winner.CanonicalCharacterNameZhCn = group.Key.Canonical;
            winner.Aliases = string.Join(", ", aliases);
            if (winner.Month is < 1 or > 12 || winner.Day < 1 || winner.Day > DateTime.DaysInMonth(2024, winner.Month))
                winner.Enabled = false;
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> UpsertAsync(OfficialBirthdayCandidate candidate, CancellationToken cancellationToken)
    {
        var isUnknown = candidate.IsUnknown || candidate.Month == 0 || candidate.Day == 0;
        var scopedRecords = await database.Birthdays.Where(x => x.Franchise == candidate.Franchise).ToListAsync(cancellationToken);
        var record = scopedRecords.FirstOrDefault(x =>
            string.Equals(x.Character, candidate.Character, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(BirthdayCharacterNameMap.Resolve(x.Character), candidate.Character, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(BirthdayCharacterNameMap.Resolve(x.CanonicalCharacterNameZhCn), candidate.Character, StringComparison.OrdinalIgnoreCase));
        var rosterAlias = candidate.Source.Equals("NteOfficialRoster", StringComparison.OrdinalIgnoreCase)
            ? ExtractRosterAlias(candidate.Evidence)
            : null;
        if (record is null && !string.IsNullOrWhiteSpace(rosterAlias))
        {
            record = scopedRecords.FirstOrDefault(x =>
                string.Equals(x.Character, rosterAlias, StringComparison.OrdinalIgnoreCase) ||
                x.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(rosterAlias, StringComparer.OrdinalIgnoreCase) ||
                x.Evidence.Contains($"Official NTE roster slot: {rosterAlias}", StringComparison.OrdinalIgnoreCase));
        }
        if (record is null)
        {
            database.Birthdays.Add(new BirthdayEntity { Character = candidate.Character, CanonicalCharacterNameZhCn = candidate.Character, Aliases = rosterAlias ?? string.Empty, Franchise = candidate.Franchise, Month = candidate.Month, Day = candidate.Day, Source = candidate.Source, SourceTier = candidate.Source.Contains("Official", StringComparison.OrdinalIgnoreCase) ? "official" : "unknown", SourceUrl = candidate.SourceUrl, Evidence = candidate.Evidence, VerificationStatus = isUnknown ? VerificationStatus.Unverified : VerificationStatus.VerifiedOfficial, VerifiedAt = candidate.FetchedAt, Enabled = !isUnknown });
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        var changed = record.Month != candidate.Month || record.Day != candidate.Day || record.Evidence != candidate.Evidence || record.SourceUrl != candidate.SourceUrl || record.VerificationStatus != (isUnknown ? VerificationStatus.Unverified : VerificationStatus.VerifiedOfficial);
        record.Month = candidate.Month; record.Day = candidate.Day; record.Character = candidate.Character; record.CanonicalCharacterNameZhCn = candidate.Character; record.Aliases = MergeAlias(record.Aliases, rosterAlias); record.Source = candidate.Source; record.SourceTier = candidate.Source.Contains("Official", StringComparison.OrdinalIgnoreCase) ? "official" : record.SourceTier; record.SourceUrl = candidate.SourceUrl; record.Evidence = candidate.Evidence; record.VerificationStatus = isUnknown ? VerificationStatus.Unverified : VerificationStatus.VerifiedOfficial; record.VerifiedAt = candidate.FetchedAt;
        if (isUnknown) record.Enabled = false;
        await database.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private static string? ExtractRosterAlias(string evidence)
    {
        const string prefix = "Official NTE roster slot: ";
        var start = evidence.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var value = evidence[(start + prefix.Length)..];
        var end = value.IndexOf(';');
        return (end >= 0 ? value[..end] : value).Trim();
    }

    private static string MergeAlias(string existing, string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return existing;
        return string.Join(", ", existing.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Append(alias).Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record BirthdayCoverageSnapshot(string Franchise, int Total, int VerifiedDates, int UnknownOrUnverified, int Enabled);
public sealed record BirthdayCoverageResult(string Franchise, int Total, int Known, int Unknown, int Pending);
public sealed record BirthdayRefreshResult(int Attempted, int Changed, int VerifiedDates, int Unknown, int Failed, IReadOnlyList<string> Failures);

public sealed record BirthdaySourceRequest(int EntryPageId, string Franchise);
public sealed record ThirdPartyBirthdaySourceRequest(string Franchise, string Provider, string Url);

public static class BirthdaySourceCatalog
{
    // HoYoLAB HoYoWiki's current Genshin character entry pages. The provider
    // still reads each page at runtime, so the birthday value remains sourced
    // from the official response and can become UNKNOWN when the field is absent.
    public static IReadOnlyList<BirthdaySourceRequest> Default { get; } =
        Enumerable.Range(1, 51)
            .Select(id => new BirthdaySourceRequest(id, "GENSHIN"))
            .ToArray();

    public static IReadOnlyList<BirthdaySourceRequest> Load(string configDirectory)
    {
        var path = Path.Combine(configDirectory, "birthday_sources.json");
        if (!File.Exists(path)) return Default;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<BirthdaySourceRequest>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
}
