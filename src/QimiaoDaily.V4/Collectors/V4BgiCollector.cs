using QimiaoDaily.Collectors;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Collectors;

public sealed class V4BgiCollector(V4Repository repository, HttpClient client)
{
    public async Task<int> CollectAsync(DateTimeOffset reportTime, CancellationToken cancellationToken = default)
    {
        var settings = repository.Read<V4Settings>("data", "settings.json");
        var provider = new GitHubCommitProvider(client);
        var total = 0;
        var statuses = repository.ReadOr(new List<ProviderStatusRecord>(), "collected", "provider-status.json")
            .Where(x => !x.Provider.StartsWith("GitHub:", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var repositoryName in settings.BgiRepositories)
        {
            try
            {
                var commits = await provider.CollectAsync(repositoryName, reportTime, cancellationToken);
                var records = commits.Select(x => new BgiCommitRecord(x.Repository, x.Sha, x.Subject, x.Url, x.CommitterDate ?? x.AuthorDate, reportTime)).ToList();
                var file = repositoryName.Contains("scripts", StringComparison.OrdinalIgnoreCase) ? "bgi-scripts.json" : "bgi-main.json";
                repository.Write(records, "collected", file);
                statuses.Add(new("GitHub:" + repositoryName, "HEALTHY", $"Collected {records.Count} commit(s).", reportTime));
                total += records.Count;
            }
            catch (Exception ex)
            {
                statuses.Add(new("GitHub:" + repositoryName, "FAILED", ex.Message, reportTime, UsedCachedData: true));
            }
        }
        repository.Write(statuses, "collected", "provider-status.json");
        return total;
    }
}
