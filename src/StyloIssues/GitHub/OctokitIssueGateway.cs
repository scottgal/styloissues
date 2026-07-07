using Octokit;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed partial class OctokitIssueGateway
{
    private readonly StyloIssuesOptions _o;
    private readonly IGitHubAppTokenProvider _tokens;
    private readonly Func<string, IGitHubClient> _clientFactory;

    public OctokitIssueGateway(IOptions<StyloIssuesOptions> o, IGitHubAppTokenProvider tokens,
        Func<string, IGitHubClient> clientFactory)
    { _o = o.Value; _tokens = tokens; _clientFactory = clientFactory; }

    private async Task<IGitHubClient> ClientAsync(CancellationToken ct) =>
        _clientFactory(await _tokens.GetInstallationTokenAsync(ct));

    private string LabelFor(string category) =>
        _o.CategoryLabels.TryGetValue(category, out var l) ? l : category;

    public async Task<IssueDetail> CreateIssueAsync(NewIssueRequest req, ReporterContext reporter, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var body = ReporterMarker.Embed(req.Body + AttributionFooter.Build(reporter), reporter.Marker);
        var create = new NewIssue(req.Title) { Body = body };
        create.Labels.Add(LabelFor(req.Category));
        var issue = await client.Issue.Create(_o.RepoOwner, _o.RepoName, create);
        return Map(issue, Array.Empty<Abstractions.IssueComment>());
    }

    internal static IssueDetail Map(Issue i, IReadOnlyList<Abstractions.IssueComment> comments) =>
        new(i.Number, i.Title, i.State.StringValue, i.UpdatedAt ?? i.CreatedAt, i.HtmlUrl, i.Body ?? "", comments);
}
