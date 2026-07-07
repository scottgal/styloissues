using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StyloIssues.Abstractions;

namespace StyloIssues.UI.Tests;

// ---------------------------------------------------------------------------
// Stubs (file-scoped to avoid conflicts with FeedbackEndpointsTests stubs)
// ---------------------------------------------------------------------------

file sealed class ListStubUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? StableId => "demo-stable-id";
    public string DisplayName => "Demo User";
    public string? GitHubLogin => null;
}

file sealed class ListStubIssueReader : IIssueReader
{
    private static readonly IReadOnlyList<IssueSummary> DemoData =
    [
        new IssueSummary(1, "Demo Bug Report", "open",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "https://example.com/1"),
    ];

    public Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
        => Task.FromResult<IssueDetail?>(null);

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
        => Task.FromResult(DemoData);

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
        => Task.FromResult(DemoData);

    public void Invalidate(int number) { }
    public void InvalidateAll() { }
}

// ---------------------------------------------------------------------------
// Factory: boots the sample's real Program so GET /feedback renders through
// the actual <sb-feedback-list> TagHelper -> FeedbackListViewComponent ->
// FeedbackList/Default.cshtml chain. IIssueReader and ICurrentUser are
// overridden with stubs so no GitHub API call is made.
// ---------------------------------------------------------------------------

public sealed class ListTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIssueReader, ListStubIssueReader>();
            services.AddSingleton<ICurrentUser, ListStubUser>();
            // Ensure MarkerKey is non-empty so ReporterMarker.Compute does not throw.
            services.Configure<StyloIssuesOptions>(o => o.MarkerKey = "test-marker-key");
        });
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class ListRendersTests : IClassFixture<ListTestFactory>
{
    private readonly HttpClient _client;

    public ListRendersTests(ListTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_feedback_renders_through_tag_helper_and_view_component()
    {
        var resp = await _client.GetAsync("/feedback");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.ToString());
        var body = await resp.Content.ReadAsStringAsync();
        // Stub data flows through IIssueReader -> FeedbackListViewComponent -> Default.cshtml:
        Assert.Contains("Demo Bug Report", body);
        // Stable marker from FeedbackList/Default.cshtml: <ul class="sb-issue-list">
        Assert.Contains("sb-issue-list", body);
    }
}
