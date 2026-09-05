using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>
/// Human review gate for birthday candidates. The refresh provider may only
/// create candidates; this service is the single path that changes Enabled.
/// </summary>
public sealed class BirthdayReviewService(QimiaoDailyDbContext database)
{
    public async Task<IReadOnlyList<BirthdayReviewRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await database.Birthdays.AsNoTracking()
            .OrderBy(x => x.Month == 0 ? 13 : x.Month)
            .ThenBy(x => x.Day)
            .ThenBy(x => x.Franchise)
            .ThenBy(x => x.Character)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var item = await database.Birthdays.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Birthday candidate {id} was not found.");

        if (enabled && !CanEnable(item))
            throw new InvalidOperationException("只有填写了合法月日的生日才能启用。");

        item.Enabled = enabled;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SaveManualAsync(Guid? id, string character, string franchise, int month, int day,
        string source, string sourceUrl, string evidence, VerificationStatus verificationStatus,
        CancellationToken cancellationToken = default)
    {
        ValidateManual(character, franchise, month, day, source, sourceUrl, evidence, verificationStatus);
        BirthdayEntity? item = id is null ? null : await database.Birthdays.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        if (id is not null && item is null) throw new KeyNotFoundException($"Birthday candidate {id} was not found.");
        var normalizedCharacter = character.Trim();
        var normalizedFranchise = franchise.Trim();
        var existingId = item?.Id ?? Guid.Empty;
        if (await database.Birthdays.AnyAsync(x => x.Id != existingId && x.Character == normalizedCharacter && x.Franchise == normalizedFranchise, cancellationToken))
            throw new InvalidOperationException("相同角色和系列的生日候选已存在，请编辑现有记录。");
        if (item is null)
        {
            item = new BirthdayEntity { Enabled = true };
            database.Birthdays.Add(item);
        }

        item.Character = normalizedCharacter;
        item.Franchise = normalizedFranchise;
        item.Month = month;
        item.Day = day;
        item.Source = source.Trim();
        item.SourceUrl = sourceUrl.Trim();
        item.Evidence = evidence.Trim();
        item.VerificationStatus = verificationStatus;
        item.VerifiedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public static bool CanEnable(BirthdayEntity item)
    {
        if (item.Month is < 1 or > 12) return false;
        return item.Day >= 1 && item.Day <= DateTime.DaysInMonth(2024, item.Month);
    }

    private static void ValidateManual(string character, string franchise, int month, int day, string source, string sourceUrl, string evidence, VerificationStatus verificationStatus)
    {
        if (string.IsNullOrWhiteSpace(character)) throw new ArgumentException("角色不能为空。");
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month)) throw new ArgumentException("生日日期不是合法的月日。");
    }

    private static BirthdayReviewRecord ToRecord(BirthdayEntity item) => new(
        item.Id,
        item.Character,
        item.Franchise,
        item.Month,
        item.Day,
        item.Source,
        item.SourceUrl,
        item.Evidence,
        item.VerificationStatus,
        item.VerifiedAt,
        item.Enabled,
        CanEnable(item));
}

public sealed record BirthdayReviewRecord(
    Guid Id,
    string Character,
    string Franchise,
    int Month,
    int Day,
    string Source,
    string SourceUrl,
    string Evidence,
    VerificationStatus VerificationStatus,
    DateTimeOffset VerifiedAt,
    bool Enabled,
    bool CanEnable);
