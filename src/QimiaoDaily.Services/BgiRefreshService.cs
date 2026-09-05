using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Collectors;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

public sealed class BgiRefreshService(QimiaoDailyDbContext database, GitHubCommitProvider provider)
{
    public async Task<int> RefreshAsync(string repository, DateTimeOffset reportTime, CancellationToken cancellationToken = default)
    {
        var commits = await provider.CollectAsync(repository, reportTime, cancellationToken);
        var existing = await database.GitCommitRecords
            .Where(x => x.Repository == repository)
            .ToDictionaryAsync(x => x.Sha, cancellationToken);

        // A refresh represents the complete BGI window for this repository.  Its
        // contents are therefore always the entries used in the automatic report.
        foreach (var record in existing.Values)
            record.SelectedForReport = false;

        foreach (var commit in commits)
        {
            if (existing.TryGetValue(commit.Sha, out var record))
            {
                record.SelectedForReport = true;
                continue;
            }

            database.GitCommitRecords.Add(new GitCommitRecord
            {
                Repository = commit.Repository,
                Sha = commit.Sha,
                Subject = commit.Subject,
                Body = commit.Body,
                Author = commit.Author,
                AuthorDate = commit.AuthorDate,
                CommitterDate = commit.CommitterDate,
                PullRequestNumber = commit.PullRequestNumber,
                PullRequestUrl = commit.PullRequestUrl,
                Url = commit.Url,
                FetchedAt = DateTimeOffset.UtcNow,
                SelectedForReport = true
            });
        }

        return await database.SaveChangesAsync(cancellationToken);
    }
}
