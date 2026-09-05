using QimiaoDaily.Collectors;
using QimiaoDaily.Data;
namespace QimiaoDaily.Services;
public sealed class NteRefreshService(QimiaoDailyDbContext database,NteBilibiliOfficialProvider provider)
{
 public async Task<bool> ImportVerifiedVideoAsync(string bvid,CancellationToken cancellationToken=default){var candidate=await provider.VerifyOfficialVideoAsync(bvid,cancellationToken);return await new OfficialVideoImportService(database).ImportAsync(candidate,cancellationToken);}
}
