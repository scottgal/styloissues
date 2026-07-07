using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
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
// Factory: minimal host stub; GET /feedback not yet mapped (RED state)
// ---------------------------------------------------------------------------

public sealed class ListTestFactory : WebApplicationFactory<TestStartup>
{
    protected override IHostBuilder? CreateHostBuilder() =>
        Host.CreateDefaultBuilder();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureWebHost(web =>
        {
            web.UseContentRoot(AppContext.BaseDirectory);
            web.UseTestServer();
        });

        var host = builder.Build();
        host.Start();
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddRouting();
            services.Configure<StyloIssuesOptions>(o =>
            {
                o.MarkerKey = "test-marker-key";
                o.RepoOwner = "demo";
                o.RepoName = "demo";
                o.EnablePublicList = true;
            });
            services.AddSingleton<ICurrentUser, ListStubUser>();
            services.AddSingleton<IIssueReader, ListStubIssueReader>();
        });
        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(ep =>
            {
                ep.MapGet("/feedback", async ctx =>
                {
                    var reader = ctx.RequestServices.GetRequiredService<IIssueReader>();
                    var issues = await reader.ListPublicAsync(CancellationToken.None);

                    var sb = new System.Text.StringBuilder(
                        "<!doctype html><html lang=\"en\"><body><ul class=\"sb-issue-list\">");
                    foreach (var issue in issues)
                    {
                        sb.Append("<li><a href=\"/feedback/");
                        sb.Append(issue.Number);
                        sb.Append("\">");
                        sb.Append(System.Net.WebUtility.HtmlEncode(issue.Title));
                        sb.Append("</a></li>");
                    }
                    sb.Append("</ul></body></html>");

                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await ctx.Response.WriteAsync(sb.ToString());
                });
            });
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
    public async Task Get_feedback_returns_ok_and_html()
    {
        var resp = await _client.GetAsync("/feedback");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.ToString());
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Demo Bug Report", body);
    }
}
