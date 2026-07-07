# StyloIssues

[![CI](https://github.com/scottgal/styloissues/actions/workflows/ci.yml/badge.svg)](https://github.com/scottgal/styloissues/actions/workflows/ci.yml)
![version](https://img.shields.io/badge/version-0.0.2-blue)
[![NuGet](https://img.shields.io/nuget/v/StyloIssues.svg)](https://www.nuget.org/packages/StyloIssues)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![license](https://img.shields.io/badge/license-Unlicense-blue)

A GitHub-issues-backed feedback UX for ASP.NET Core. Users file bug reports and
feature requests through a first-class UI on your site; those flow two-way to
and from GitHub issues on your repo. GitHub stays the source of truth and gets
the full fix-it workflow (labels, PRs, CI); your site provides a nicer front door.

Reusable and framework-agnostic: pluggable identity (`ICurrentUser`), pluggable
form policy (`IFeedbackFormPolicy`), an optional read-model (`IIssueStore`), and
an optional diagnostic-archive attachment hook (`IIssueAttachmentSource`).
GitHub is the source of truth, so the default build needs no database.

## Packages

| Package | What it is |
|---------|------------|
| `StyloIssues.Abstractions` | Interfaces, DTOs, options, the zero-PII reporter marker. |
| `StyloIssues` | Octokit GitHub gateway, read cache, webhook + reconciler, `AddStyloIssues`. |
| `StyloIssues.UI` | Razor Class Library: `sb-feedback-*` TagHelpers, endpoints, HTMX + Alpine UI. |

`StyloIssues.UI` depends on the other two, so installing it pulls the whole chain.

## Install

```bash
dotnet add package StyloIssues.UI
```

## Getting started

Three steps: register the services, map the endpoints, and add two host pages
that embed the TagHelpers.

### 1. Register + map (Program.cs)

```csharp
using StyloIssues;      // AddStyloIssues
using StyloIssues.UI;   // AddStyloIssuesUi, MapStyloIssues

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStyloIssues(o =>
{
    o.RepoOwner = "your-org";
    o.RepoName  = "your-repo";
    // Simplest auth: a GitHub token (see "Authenticating to GitHub" below).
    o.PersonalAccessToken = builder.Configuration["StyloIssues:Token"] ?? "";
    // Secrets: bind from config / env / user-secrets, never hard-code.
    o.MarkerKey     = builder.Configuration["StyloIssues:MarkerKey"] ?? "";
    o.WebhookSecret = builder.Configuration["StyloIssues:WebhookSecret"] ?? "";
});

builder.Services.AddStyloIssuesUi();     // TagHelpers + ViewComponents + antiforgery
builder.Services.AddRazorPages();        // (or MVC) for your host-owned pages

// Tell StyloIssues who the current user is (see below).
builder.Services.AddScoped<StyloIssues.Abstractions.ICurrentUser, MyCurrentUser>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();
app.MapStyloIssues();   // POST write endpoints + the GitHub webhook sink

app.Run();
```

### 2. Tell StyloIssues who is filing (ICurrentUser)

Project it from your existing authentication. `StableId` is an opaque, stable
per-user id (only its `HMAC` ever reaches GitHub, so it stays zero-PII).

```csharp
using StyloIssues.Abstractions;

public sealed class MyCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? U => http.HttpContext?.User;
    public bool IsAuthenticated => U?.Identity?.IsAuthenticated ?? false;
    public string? StableId     => U?.FindFirst("sub")?.Value;        // stable, opaque
    public string DisplayName   => U?.Identity?.Name ?? "Anonymous";
    public string? GitHubLogin  => U?.FindFirst("github_login")?.Value; // optional, for @mention
}
```

### 3. Add the two host pages (embed the TagHelpers)

The package renders through TagHelpers so you keep full control of your layout.
Register the TagHelpers once in `Pages/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, StyloIssues.UI
```

`Pages/Feedback.cshtml`:

```cshtml
@page "/feedback"
<h1>Feedback</h1>
<sb-feedback-form />
<sb-feedback-list />
```

`Pages/FeedbackDetail.cshtml`:

```cshtml
@page "/feedback/{number:int}"
@{ var number = Convert.ToInt32(RouteData.Values["number"]); }
<sb-feedback-detail number="@number" />
```

Finally, in your layout, load the vendored assets and attach the antiforgery
token to HTMX requests (see [Security notes](#security-notes)):

```cshtml
<script src="~/_content/StyloIssues.UI/htmx.min.js"></script>
<script defer src="~/_content/StyloIssues.UI/alpine.min.js"></script>
@Html.AntiForgeryToken()
<script>
document.body.addEventListener('htmx:configRequest', e => {
  const t = document.querySelector('input[name="__RequestVerificationToken"]');
  if (t) e.detail.headers['RequestVerificationToken'] = t.value;
});
</script>
```

`samples/StyloIssues.Sample` is a complete, runnable reference for all of the above.

## Authenticating to GitHub

Two options; pick one.

- **Personal Access Token (simplest, single-repo / self-host).** Set
  `o.PersonalAccessToken` to a token with `issues` read/write on the repo (a
  fine-grained PAT scoped to the one repo is ideal). The token is used directly
  and the GitHub-App flow is skipped.
- **GitHub App (multi-tenant / higher rate limits).** Leave
  `PersonalAccessToken` empty and set `o.AppId`, `o.InstallationId`, and
  `o.PrivateKeyPem`. StyloIssues mints an installation token per request.

All secrets come from options; bind them from configuration, environment
variables, or user-secrets. Never commit them.

## Options

| Option | Purpose | Default |
|--------|---------|---------|
| `RepoOwner` / `RepoName` | The GitHub repo issues live in. | (required) |
| `PersonalAccessToken` | GitHub token; when set, skips the App flow. | `""` |
| `AppId` / `InstallationId` / `PrivateKeyPem` | GitHub App credentials (used when no PAT). | `0` / `0` / `""` |
| `MarkerKey` | HMAC key for the opaque per-user reporter marker. | `""` (required) |
| `WebhookSecret` | HMAC secret for the GitHub webhook. | `""` |
| `EnablePublicList` | Let anonymous visitors list issues. | `true` |
| `CacheTtl` | Read-cache lifetime. | 2 min |
| `ReconcileInterval` | Webhook-backstop refresh cadence. | 10 min |
| `CategoryLabels` | Map form categories to GitHub labels. | `{}` |

## Verdict-adaptive form (IFeedbackFormPolicy)

The form can adapt to the visitor. `IFeedbackFormPolicy.Evaluate` returns a
`FeedbackFormState`: `Full` (normal form), `ChallengeGated` (host handles a
challenge), or `Bare` (hide submit, show a message). The write endpoints enforce
`Bare` server-side with a `403` before creating anything, so hiding the button is
never the only gate. The default policy returns `Full`; bind your own to plug in
a bot verdict or moderation signal.

## Host route convention

- `GET /feedback` embeds `<sb-feedback-form>` and `<sb-feedback-list />`.
- `GET /feedback/{number:int}` embeds `<sb-feedback-detail number="@number" />`.

These GET pages are host-owned (see step 3) so you control layout and chrome.
The package owns the `POST` write endpoints and the webhook, mapped by
`MapStyloIssues()`, and redirects to `/feedback/{number}` after a create.

## IIssueStore

`IIssueStore` is a forward-declared seam for a host-supplied read-model (e.g. a
SQLite or PostgreSQL projection). The default build binds a no-op; no production
wiring drives it in this drop. Implement and register it only if you need a local
query surface beyond what the GitHub API already provides via `IIssueReader`.

## Security notes

**CSRF:** Antiforgery is enforced by the package. `AddStyloIssuesUi` registers the
antiforgery service with `HeaderName = "RequestVerificationToken"`. The form
partials render the token as a hidden field via `@Html.AntiForgeryToken()`. The
write handlers (`/feedback/new` and `/feedback/{n}/comment`) call
`antiforgery.ValidateRequestAsync(context)` after the auth and bot-gate checks.
`DisableAntiforgery()` is applied only to the HMAC-signed webhook endpoint
(`/feedback/webhook`), which receives server-to-server delivery from GitHub.

Hosts using HTMX must attach the token to HTMX requests with the standard
`htmx:configRequest` listener shown in step 3. No-JS form submissions use the
hidden field directly.

**Zero-PII:** the only reporter identity that reaches GitHub or the reporter
marker is `HMAC-SHA256(MarkerKey, StableId)`, never the raw user id.

## License

Released into the public domain under [The Unlicense](LICENSE).
