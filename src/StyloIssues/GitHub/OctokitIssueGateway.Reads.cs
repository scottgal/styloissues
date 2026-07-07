using Octokit;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed partial class OctokitIssueGateway : IIssueGateway
{
    public async Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var issue = await client.Issue.Get(_o.RepoOwner, _o.RepoName, number);
        if (issue is null) return null;
        var raw = await client.Issue.Comment.GetAllForIssue(_o.RepoOwner, _o.RepoName, number);
        var comments = raw.Select(c => new Abstractions.IssueComment(
            c.User.Login, c.Body, c.CreatedAt, c.User.Type == AccountType.Bot)).ToList();
        return Map(issue, comments);
    }

    public async Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var req = new SearchIssuesRequest($"\"{ReporterMarker.SearchTerm(marker)}\"")
        { Repos = { $"{_o.RepoOwner}/{_o.RepoName}" } };
        var res = await client.Search.SearchIssues(req);
        return res.Items.Select(i => (IssueSummary)Map(i, Array.Empty<Abstractions.IssueComment>())).ToList();
    }

    public async Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var all = await client.Issue.GetAllForRepository(_o.RepoOwner, _o.RepoName,
            new RepositoryIssueRequest { State = ItemStateFilter.All });
        return all.Select(i => (IssueSummary)Map(i, Array.Empty<Abstractions.IssueComment>())).ToList();
    }

    public async Task AddCommentAsync(int number, string body, ReporterContext reporter, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        await client.Issue.Comment.Create(_o.RepoOwner, _o.RepoName, number, body + AttributionFooter.Build(reporter));
    }
}
