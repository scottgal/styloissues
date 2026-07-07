using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StyloIssues.Abstractions;
using StyloIssues.Ui;
using StyloIssues.Webhook;

namespace StyloIssues.Ui.Tests;

// ---------------------------------------------------------------------------
// Stubs
// ---------------------------------------------------------------------------

file sealed class AuthenticatedStubUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? StableId => "test-stable-id-001";
    public string DisplayName => "Test User";
    public string? GitHubLogin => "testuser";
}

file sealed class BareStubPolicy : IFeedbackFormPolicy
{
    public FeedbackVerdictView Evaluate(HttpContext context, ICurrentUser user)
        => new(FeedbackFormState.Bare, "TestBot", "Crawler", 0.95, "high", "policy-test");
}

file sealed class StubIssueGateway : IIssueGateway
{
    public Task<IssueDetail> CreateIssueAsync(NewIssueRequest req, ReporterContext reporter, CancellationToken ct)
        => Task.FromResult(new IssueDetail(1, req.Title, "open", DateTimeOffset.UtcNow, "https://example.com/1", req.Body, []));

    public Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
        => Task.FromResult<IssueDetail?>(null);

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public Task AddCommentAsync(int number, string body, ReporterContext reporter, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class StubIssueReader : IIssueReader
{
    public Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
        => Task.FromResult<IssueDetail?>(null);

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

    public void Invalidate(int number) { }
    public void InvalidateAll() { }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

// Marker type used as TEntryPoint. The real host configuration lives in TestFactory.
public sealed class TestStartup { }

public sealed class TestFactory : WebApplicationFactory<TestStartup>
{
    // Override CreateHostBuilder so the factory uses the IHostBuilder code path
    // rather than DeferredHostBuilder/HostFactoryResolver, which requires a real
    // WebApplication entry point in the assembly.
    protected override IHostBuilder? CreateHostBuilder() =>
        Host.CreateDefaultBuilder();

    // Services and middleware for the test host.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(ConfigureTestServices);
        builder.Configure(ConfigureTestApp);
    }

    // Override CreateHost to substitute a valid content root.
    // The base implementation calls SetContentRoot which computes an incorrect
    // path for library assemblies that are not a real application entry point.
    // We set it to AppContext.BaseDirectory (the bin output directory) which
    // always exists, and register UseTestServer so the host stays in-process.
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

    private static void ConfigureTestServices(IServiceCollection services)
    {
        services.AddRouting();
        services.Configure<StyloIssues.Abstractions.StyloIssuesOptions>(
            o => o.MarkerKey = "test-marker-key");

        services.AddSingleton<ICurrentUser, AuthenticatedStubUser>();
        services.AddSingleton<IFeedbackFormPolicy, BareStubPolicy>();
        services.AddSingleton<IIssueGateway, StubIssueGateway>();
        services.AddSingleton<IIssueReader, StubIssueReader>();
        services.AddSingleton<WebhookHandler>();

        services.AddStyloIssuesUi();
    }

    private static void ConfigureTestApp(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(ep => ep.MapStyloIssues());
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class FeedbackControllerTests : IClassFixture<TestFactory>
{
    private readonly HttpClient _client;

    public FeedbackControllerTests(TestFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Post_new_is_refused_when_policy_is_bare()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["title"]    = "test title",
            ["body"]     = "test body",
            ["category"] = "bug",
        });

        var response = await _client.PostAsync("/feedback/new", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
