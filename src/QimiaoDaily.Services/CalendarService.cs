using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class CalendarService(QimiaoDailyDbContext database)
{
    public async Task<IReadOnlyList<CalendarOccurrence>> ForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var birthdays = (await database.Birthdays.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new BirthdayRecord(x.Character, x.Franchise, x.Month, x.Day, x.Source, x.SourceUrl, x.Evidence, x.VerificationStatus, x.VerifiedAt, x.Enabled));
        var anniversaries = (await database.Anniversaries.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new AnniversaryRecord(x.Title, x.StartedOn, x.Enabled));
        var occurrences = ChineseCalendarEngine.Occurrences(date, birthdays, anniversaries).ToList();
        occurrences.AddRange(await database.CalendarEvents.AsNoTracking().Where(x => x.EventDate == date && x.Enabled && x.Kind != "GAME").Select(x => new CalendarOccurrence(x.Kind, x.Title, x.EventDate, x.Detail)).ToListAsync(cancellationToken));
        return occurrences;
    }

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<CalendarOccurrence>>> ForYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var birthdays = (await database.Birthdays.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new BirthdayRecord(x.Character, x.Franchise, x.Month, x.Day, x.Source, x.SourceUrl, x.Evidence, x.VerificationStatus, x.VerifiedAt, x.Enabled)).ToList();
        var anniversaries = (await database.Anniversaries.AsNoTracking().ToListAsync(cancellationToken)).Select(x => new AnniversaryRecord(x.Title, x.StartedOn, x.Enabled)).ToList();
        var custom = await database.CalendarEvents.AsNoTracking().Where(x => x.Enabled && x.Kind != "GAME" && x.EventDate.Year == year).ToListAsync(cancellationToken);
        var result = new Dictionary<DateOnly, IReadOnlyList<CalendarOccurrence>>();
        for (var date = new DateOnly(year, 1, 1); date.Year == year; date = date.AddDays(1))
        {
            var items = ChineseCalendarEngine.Occurrences(date, birthdays, anniversaries).ToList();
            items.AddRange(custom.Where(x => x.EventDate == date).Select(x => new CalendarOccurrence(x.Kind, x.Title, date, x.Detail)));
            result[date] = items;
        }
        return result;
    }

    public async Task<CalendarEventEntity> AddCustomEventAsync(DateOnly date, string title, string kind = "MEMORIAL", string? detail = null, string source = "MANUAL", string? sourceUrl = null, CancellationToken cancellationToken = default)
    {
        var item = new CalendarEventEntity { EventDate = date, Title = title.Trim(), Kind = kind.Trim(), Detail = detail, Source = source, SourceUrl = sourceUrl };
        database.CalendarEvents.Add(item);
        await database.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateCustomEventAsync(Guid id, DateOnly date, string title, string kind, string? detail, bool enabled, CancellationToken cancellationToken = default)
    {
        var item = await database.CalendarEvents.SingleAsync(x => x.Id == id, cancellationToken);
        item.EventDate = date; item.Title = title.Trim(); item.Kind = kind.Trim(); item.Detail = detail; item.Enabled = enabled;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCustomEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await database.CalendarEvents.SingleAsync(x => x.Id == id, cancellationToken);
        database.CalendarEvents.Remove(item);
        await database.SaveChangesAsync(cancellationToken);
    }
}
