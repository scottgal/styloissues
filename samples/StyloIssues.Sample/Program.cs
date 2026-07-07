using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.Sample;
using StyloIssues.UI;

var builder = WebApplication.CreateBuilder(args);

// Register StyloIssues UI components (ViewComponents, TagHelpers).
builder.Services.AddStyloIssuesUi();

// Host-owned Razor Pages (provides the GET /feedback page).
builder.Services.AddRazorPages();

// Demo services: offline, no GitHub credentials required.
// Swap DemoCurrentUser for your real authentication integration.
// Swap DemoIssueReader for the real AddStyloIssues() registration when credentials are available.
builder.Services.AddSingleton<ICurrentUser, DemoCurrentUser>();
builder.Services.AddSingleton<IIssueReader, DemoIssueReader>();
builder.Services.AddSingleton<IFeedbackFormPolicy, DefaultFeedbackFormPolicy>();

builder.Services.Configure<StyloIssuesOptions>(o =>
{
    o.RepoOwner = builder.Configuration["StyloIssues:RepoOwner"] ?? "demo";
    o.RepoName = builder.Configuration["StyloIssues:RepoName"] ?? "demo";
    o.MarkerKey = builder.Configuration["StyloIssues:MarkerKey"] ?? "change-this-in-production";
    o.EnablePublicList = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();

// Allow WebApplicationFactory to discover this entry point in integration tests.
public partial class Program { }
