using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StyloIssues.Abstractions;

namespace StyloIssues.UI.Tests;

// ---------------------------------------------------------------------------
// Stubs
// ---------------------------------------------------------------------------

file sealed class DetailStubUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? StableId => "detail-stable-id";
    public string DisplayName => "Detail User";
    public string? GitHubLogin => null;
}

file sealed class DetailStubIssueReader : IIssueReader
{
    private static readonly IssueDetail Demo = new(
        1,
        "Detail Test Issue",
        "open",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        "https://github.com/demo/demo/issues/1",
        "This is the demo body.",
        []);

    public Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
        => Task.FromResult<IssueDetail?>(number == 1 ? Demo : null);

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public void Invalidate(int number) { }
    public void InvalidateAll() { }
}

// ---------------------------------------------------------------------------
// Factory: boots the sample Program, overrides IIssueReader with stub so no
// GitHub API calls are made, and verifies GET /feedback/{n} renders through
// FeedbackDetailViewComponent -> FeedbackDetail/Default.cshtml.
// ---------------------------------------------------------------------------

public sealed class DetailTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIssueReader, DetailStubIssueReader>();
            services.AddSingleton<ICurrentUser, DetailStubUser>();
            services.Configure<StyloIssuesOptions>(o => o.MarkerKey = "test-marker-key");
        });
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class DetailRendersTests : IClassFixture<DetailTestFactory>
{
    private readonly HttpClient _client;

    public DetailRendersTests(DetailTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_feedback_detail_renders_through_tag_helper_and_view_component()
    {
        var resp = await _client.GetAsync("/feedback/1");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.ToString());
        var body = await resp.Content.ReadAsStringAsync();
        // Unique marker from FeedbackDetail/Default.cshtml (rendered when issue is found):
        // <article class="sb-issue-detail">
        Assert.Contains("sb-issue-detail", body);
        Assert.Contains("Detail Test Issue", body);
    }

    [Fact]
    public async Task Get_feedback_detail_not_found_renders_sb_not_found_marker()
    {
        var resp = await _client.GetAsync("/feedback/999");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        // Unique marker from FeedbackDetail/Default.cshtml (rendered when issue is null):
        // <div class="sb-not-found">
        Assert.Contains("sb-not-found", body);
    }
}
