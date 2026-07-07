using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.Sample;
using StyloIssues.UI;

var builder = WebApplication.CreateBuilder(args);

// Register the core StyloIssues services (GitHub gateway, caching, reconciler, webhook handler).
// Placeholder credentials are fine for the sample: DemoIssueReader below overrides the real
// CachingIssueReader, so no GitHub API calls are made. Swap in real values via appsettings.json
// or environment variables when going live.
builder.Services.AddStyloIssues(o =>
{
    o.RepoOwner      = builder.Configuration["StyloIssues:RepoOwner"]      ?? "scottgal";
    o.RepoName       = builder.Configuration["StyloIssues:RepoName"]       ?? "stylobot";
    o.MarkerKey      = builder.Configuration["StyloIssues:MarkerKey"]      ?? "change-this-in-production";
    o.WebhookSecret  = builder.Configuration["StyloIssues:WebhookSecret"]  ?? "placeholder-webhook-secret";
    o.PrivateKeyPem  = builder.Configuration["StyloIssues:PrivateKeyPem"]  ?? "placeholder-private-key-pem";
    o.AppId          = long.TryParse(builder.Configuration["StyloIssues:AppId"],          out var ai) ? ai : 0L;
    o.InstallationId = long.TryParse(builder.Configuration["StyloIssues:InstallationId"], out var ii) ? ii : 0L;
    o.EnablePublicList = true;
});

// Register StyloIssues UI components (ViewComponents, TagHelpers).
builder.Services.AddStyloIssuesUi();

// Host-owned Razor Pages (provides the GET /feedback page).
builder.Services.AddRazorPages();

// Demo services: offline, no GitHub credentials required.
// Swap DemoCurrentUser for your real authentication integration.
// DemoIssueReader overrides the real CachingIssueReader registered by AddStyloIssues above,
// so no GitHub calls are made in the sample. Remove these two lines when going live.
builder.Services.AddSingleton<ICurrentUser, DemoCurrentUser>();
builder.Services.AddSingleton<IIssueReader, DemoIssueReader>();
builder.Services.AddSingleton<IFeedbackFormPolicy, DefaultFeedbackFormPolicy>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();
app.MapStyloIssues();

app.Run();

// Allow WebApplicationFactory to discover this entry point in integration tests.
public partial class Program { }
