using Moq;
using Octokit;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class OctokitIssueGatewayTests
{
    static IOptions<StyloIssuesOptions> Opts() =>
        Options.Create(new StyloIssuesOptions { RepoOwner = "scottgal", RepoName = "stylobot" });

    [Fact]
    public async Task ListByReporter_searches_issues_with_marker_term()
    {
        var search = new Mock<ISearchClient>();
        search.Setup(s => s.SearchIssues(It.IsAny<SearchIssuesRequest>()))
              .ReturnsAsync(new SearchIssuesResult(0, false, new List<Issue>()));
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Search).Returns(search.Object);

        var tokens = new Mock<IGitHubAppTokenProvider>();
        tokens.Setup(t => t.GetInstallationTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        var sut = new OctokitIssueGateway(Opts(), tokens.Object, _ => client.Object);

        await sut.ListByReporterAsync("abc123", default);

        search.Verify(s => s.SearchIssues(It.Is<SearchIssuesRequest>(
            r => r.Term.Contains(ReporterMarker.SearchTerm("abc123")))), Times.Once);
    }
}
