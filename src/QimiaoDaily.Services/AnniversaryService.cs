using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class AnniversaryService(QimiaoDailyDbContext database)
{
    public async Task<AnniversaryEntity> SaveAsync(Guid? id, AnniversaryInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ArgumentException("纪念日名称不能为空。", nameof(input));
        var entity = id is { } existing
            ? await database.Anniversaries.SingleAsync(x => x.Id == existing, cancellationToken)
            : new AnniversaryEntity();
        entity.Title = input.Title.Trim();
        entity.StartedOn = input.StartedOn;
        entity.Notes = input.Notes?.Trim() ?? string.Empty;
        entity.Enabled = true;
        entity.DataOrigin = DataOrigin.Manual;
        entity.UserConfirmed = true;
        if (id is null) database.Anniversaries.Add(entity);
        database.ManualDataAudits.Add(new ManualDataAuditEntity
        {
            EntityType = "Anniversary",
            EntityId = entity.Id,
            Action = id is null ? "CREATE" : "UPDATE"
        });
        await database.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var entity = await database.Anniversaries.SingleAsync(x => x.Id == id, cancellationToken);
        entity.Enabled = enabled;
        entity.DataOrigin = DataOrigin.Manual;
        entity.UserConfirmed = true;
        database.ManualDataAudits.Add(new ManualDataAuditEntity { EntityType = "Anniversary", EntityId = id, Action = enabled ? "ENABLE" : "DISABLE" });
        await database.SaveChangesAsync(cancellationToken);
    }
}
