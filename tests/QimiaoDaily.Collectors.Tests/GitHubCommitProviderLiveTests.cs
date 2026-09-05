using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class GitHubCommitProviderLiveTests
{
 [Fact] public async Task CollectAsync_ReadsBothRealBgiRepositories_WhenExplicitlyEnabled(){if(Environment.GetEnvironmentVariable("QIMIAO_LIVE_TESTS")!="1")return;using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var p=new GitHubCommitProvider(client);var now=DateTimeOffset.UtcNow;var a=await p.CollectAsync("babalae/better-genshin-impact",now);var b=await p.CollectAsync("babalae/bettergi-scripts-list",now);Assert.NotEmpty(a);Assert.NotEmpty(b);Assert.All(a.Concat(b),x=>Assert.False(string.IsNullOrWhiteSpace(x.Sha)));}
}
