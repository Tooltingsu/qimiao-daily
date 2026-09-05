namespace QimiaoDaily.Data;
public sealed class GitCommitRecord
{
 public Guid Id{get;set;}=Guid.NewGuid(); public string Repository{get;set;}=string.Empty; public string Sha{get;set;}=string.Empty; public string Subject{get;set;}=string.Empty; public string? Body{get;set;} public string? Author{get;set;} public DateTimeOffset? AuthorDate{get;set;} public DateTimeOffset? CommitterDate{get;set;} public int? PullRequestNumber{get;set;} public string? PullRequestUrl{get;set;} public string Url{get;set;}=string.Empty; public DateTimeOffset FetchedAt{get;set;} public bool SelectedForReport{get;set;}
}
