using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.Sample;
using StyloIssues.UI;

var builder = WebApplication.CreateBuilder(args);

// Register the core StyloIssues services (GitHub gateway, caching, reconciler, webhook handler).
// Set StyloIssues:PersonalAccessToken via user-secrets or environment variable to authenticate
// with a GitHub PAT. Set StyloIssues:RepoOwner and StyloIssues:RepoName to target a repository.
// When PersonalAccessToken is empty the GitHub App flow (AppId/InstallationId/PrivateKeyPem) is used.
builder.Services.AddStyloIssues(o =>
{
    o.PersonalAccessToken = builder.Configuration["StyloIssues:PersonalAccessToken"] ?? "";
    o.RepoOwner      = builder.Configuration["StyloIssues:RepoOwner"]      ?? "scottgal";
    o.RepoName       = builder.Configuration["StyloIssues:RepoName"]       ?? "styloissues-demo";
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

// Swap DemoCurrentUser for your real authentication integration.
builder.Services.AddSingleton<ICurrentUser, DemoCurrentUser>();
builder.Services.AddSingleton<IFeedbackFormPolicy, DefaultFeedbackFormPolicy>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();
app.MapStyloIssues();

app.Run();

// Allow WebApplicationFactory to discover this entry point in integration tests.
public partial class Program { }
