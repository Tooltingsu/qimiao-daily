using System.Net.Http.Headers;
using System.Text.Json;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Collectors;

public sealed class V4BgiCollector(V4Repository repository, HttpClient client)
{
    public Task<int> CollectAsync(DateTimeOffset reportTime, CancellationToken cancellationToken = default)
        => CollectAsync(ShanghaiClock.Date(reportTime), reportTime, cancellationToken);

    public async Task<int> CollectAsync(DateOnly date, DateTimeOffset reportTime, CancellationToken cancellationToken = default)
    {
        var settings = repository.Read<V4Settings>("data", "settings.json");
        var (start, end) = ShanghaiClock.BgiWindow(date);
        var total = 0;
        var statuses = repository.ReadOr(new List<ProviderStatusRecord>(), "collected", "provider-status.json")
            .Where(x => !x.Provider.StartsWith("GitHub:", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var repositoryName in settings.BgiRepositories)
        {
            try
            {
                var records = await FetchWindow(repositoryName, start, end, reportTime, cancellationToken);
                var file = repositoryName.Contains("scripts", StringComparison.OrdinalIgnoreCase) ? "bgi-scripts.json" : "bgi-main.json";
                repository.Write(records, "collected", file);
                statuses.Add(new("GitHub:" + repositoryName, "HEALTHY", $"{records.Count} commits in [{start:O}, {end:O}); {(reportTime < end ? "provisional before cutoff" : "complete window")}", reportTime));
                total += records.Count;
            }
            catch (Exception ex)
            {
                var file = repositoryName.Contains("scripts", StringComparison.OrdinalIgnoreCase) ? "bgi-scripts.json" : "bgi-main.json";
                statuses.Add(new("GitHub:" + repositoryName, "FAILED", ex is HttpRequestException ? ex.Message : ex.GetType().Name,
                    reportTime, repository.ReadOr(new List<BgiCommitRecord>(), "collected", file).Count > 0));
            }
        }
        repository.Write(statuses, "collected", "provider-status.json");
        repository.Write(new { date, start, end, provisional = reportTime < end, checkedAt = reportTime }, "collected", "bgi-window.json");
        return total;
    }

    private async Task<List<BgiCommitRecord>> FetchWindow(string name, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken ct)
    {
        var records = new List<BgiCommitRecord>();
        for (var page = 1; ; page++)
        {
            if (page > 100) throw new InvalidDataException("Pagination limit exceeded; refusing partial snapshot.");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{name}/commits?sha=main&since={Uri.EscapeDataString(start.UtcDateTime.ToString("O"))}&until={Uri.EscapeDataString(end.UtcDateTime.ToString("O"))}&per_page=100&page={page}");
            request.Headers.UserAgent.ParseAdd("QimiaoDaily/4.0");
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub HTTP {(int)response.StatusCode}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach (var item in json.RootElement.EnumerateArray())
            {
                var commit = item.GetProperty("commit");
                var instant = commit.GetProperty("committer").GetProperty("date").GetDateTimeOffset();
                if (instant < start || instant >= end) continue;
                records.Add(new(name, item.GetProperty("sha").GetString()!, commit.GetProperty("message").GetString()!.Split('\n')[0],
                    item.GetProperty("html_url").GetString()!, instant, now));
            }
            if (json.RootElement.GetArrayLength() < 100) break;
        }
        return records.DistinctBy(x => x.Sha).OrderBy(x => x.CommittedAt).ThenBy(x => x.Sha).ToList();
    }
}
