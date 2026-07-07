# StyloIssues Core Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable `StyloIssues` NuGet package: a GitHub-issues-backed feedback UX for ASP.NET Core with two-way sync, pluggable identity and form policy, GitHub as source of truth, and an SSR + HTMX + Alpine UI that degrades to plain HTML.

**Architecture:** Two projects. `StyloIssues.Abstractions` holds interfaces, DTOs, options, and pure helpers (no framework deps beyond `Microsoft.AspNetCore.Http.Abstractions`). `StyloIssues` holds the Octokit-backed GitHub gateway with GitHub-App auth, a short-TTL read cache (GitHub is the store; no database required), a webhook sink plus a reconciler hosted service, a default form policy, and a Razor Class Library UI mapped with one call. Hosts replace `ICurrentUser`, `IFeedbackFormPolicy`, and optionally `IIssueStore` via DI.

**Tech Stack:** .NET 10, ASP.NET Core, Razor Class Library, Octokit, GitHubJwt, `System.Security.Cryptography` HMAC, `TimeProvider`, xUnit + Moq, HTMX + Alpine (vendored).

## Global Constraints

- Target framework: `net10.0` (all projects).
- Test stack: xUnit + Moq. Run with `dotnet test`.
- No em dashes in any code comment, doc, or UI copy; use colons, semicolons, commas, parentheses.
- No hard-coded word/label lists in C#; data (category-to-label map, etc.) lives in options/config, code is the dispatcher.
- All secrets (App private key, webhook secret, marker HMAC key, OAuth client secret) come from options bound to env / secret store, never a config-file default value.
- GitHub is the source of truth. The default build has no database. Any local store is an optional read-model bound by the host, refreshed from GitHub, never authoritative.
- Zero-PII: the only reporter identity persisted or embedded is an opaque `HMAC-SHA256(marker_key, keycloak_sub)`. No email, no personal GitHub tokens.
- Time is injected via `TimeProvider` (never `DateTimeOffset.UtcNow` directly) so token expiry and cache TTL are testable.
- Vendored front-end assets (HTMX, Alpine); no CDN references.

---

### Task 1: Solution scaffold + Abstractions (types, options, ReporterMarker)

**Files:**
- Create: `StyloIssues.sln`
- Create: `src/StyloIssues.Abstractions/StyloIssues.Abstractions.csproj`
- Create: `src/StyloIssues.Abstractions/Dtos.cs`
- Create: `src/StyloIssues.Abstractions/Interfaces.cs`
- Create: `src/StyloIssues.Abstractions/StyloIssuesOptions.cs`
- Create: `src/StyloIssues.Abstractions/ReporterMarker.cs`
- Test: `tests/StyloIssues.Abstractions.Tests/ReporterMarkerTests.cs`

**Interfaces:**
- Produces: DTOs `NewIssueRequest`, `IssueSummary`, `IssueComment`, `IssueDetail`, `ReporterContext`; enum `FeedbackFormState`; record `FeedbackVerdictView`; interfaces `ICurrentUser`, `IIssueGateway`, `IFeedbackFormPolicy`, `IIssueStore`; `StyloIssuesOptions`; static `ReporterMarker.Compute(byte[] key, string subject)`, `ReporterMarker.Embed(string body, string marker)`, `ReporterMarker.SearchTerm(string marker)`.

- [ ] **Step 1: Write the failing test**

```csharp
using StyloIssues.Abstractions;
using Xunit;

public class ReporterMarkerTests
{
    static readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes("test-marker-key-0123456789");

    [Fact]
    public void Compute_is_stable_and_opaque_for_a_subject()
    {
        var a = ReporterMarker.Compute(Key, "kc-sub-abc");
        var b = ReporterMarker.Compute(Key, "kc-sub-abc");
        Assert.Equal(a, b);
        Assert.DoesNotContain("kc-sub-abc", a);      // opaque: raw sub never present
        Assert.Matches("^[0-9a-f]{64}$", a);          // hex sha256
    }

    [Fact]
    public void Compute_differs_per_subject()
    {
        Assert.NotEqual(ReporterMarker.Compute(Key, "sub-1"), ReporterMarker.Compute(Key, "sub-2"));
    }

    [Fact]
    public void Embed_then_SearchTerm_round_trips_in_body()
    {
        var marker = ReporterMarker.Compute(Key, "sub-1");
        var body = ReporterMarker.Embed("My bug report.", marker);
        Assert.Contains(ReporterMarker.SearchTerm(marker), body);
        Assert.Contains("My bug report.", body);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Abstractions.Tests`
Expected: FAIL, `ReporterMarker` does not exist.

- [ ] **Step 3: Write minimal implementation**

`ReporterMarker.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace StyloIssues.Abstractions;

/// <summary>
/// Opaque, stable reporter identity derived from a host user id (e.g. a Keycloak
/// sub). Embedded as a hidden marker in the GitHub issue body so "my issues"
/// resolves via GitHub search with no local database and no PII.
/// </summary>
public static class ReporterMarker
{
    private const string Prefix = "sb-reporter:";

    public static string Compute(byte[] key, string subject)
    {
        using var h = new HMACSHA256(key);
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(subject));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Appends a hidden HTML-comment marker to the issue body.</summary>
    public static string Embed(string body, string marker) =>
        $"{body}\n\n<!-- {Prefix}{marker} -->";

    /// <summary>The literal GitHub search fragment that matches an embedded marker.</summary>
    public static string SearchTerm(string marker) => $"{Prefix}{marker}";
}
```

`Dtos.cs`, `Interfaces.cs`, `StyloIssuesOptions.cs`:

```csharp
namespace StyloIssues.Abstractions;

public sealed record NewIssueRequest(string Title, string Body, string Category);

public record IssueSummary(int Number, string Title, string State, DateTimeOffset UpdatedAt, string HtmlUrl);

public sealed record IssueComment(string Author, string Body, DateTimeOffset CreatedAt, bool FromApp);

public sealed record IssueDetail(
    int Number, string Title, string State, DateTimeOffset UpdatedAt, string HtmlUrl,
    string Body, IReadOnlyList<IssueComment> Comments)
    : IssueSummary(Number, Title, State, UpdatedAt, HtmlUrl);

/// <summary>Attribution context passed to the gateway on every write.</summary>
public sealed record ReporterContext(string DisplayName, string? GitHubLogin, string Marker);

public enum FeedbackFormState { Full, ChallengeGated, Bare }

public sealed record FeedbackVerdictView(
    FeedbackFormState State, string? BotName, string? BotType,
    double Probability, string ThreatBand, string? Reason);
```

```csharp
using Microsoft.AspNetCore.Http;
namespace StyloIssues.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? StableId { get; }     // opaque host user id (e.g. Keycloak sub)
    string DisplayName { get; }
    string? GitHubLogin { get; }  // present only when the user linked GitHub
}

public interface IIssueGateway
{
    Task<IssueDetail> CreateIssueAsync(NewIssueRequest req, ReporterContext reporter, CancellationToken ct);
    Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct);
    Task AddCommentAsync(int number, string body, ReporterContext reporter, CancellationToken ct);
}

public interface IFeedbackFormPolicy
{
    FeedbackVerdictView Evaluate(HttpContext context, ICurrentUser user);
}

/// <summary>Optional host-supplied read-model. Default build binds a no-op.</summary>
public interface IIssueStore
{
    Task UpsertAsync(IssueDetail issue, string? reporterMarker, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct);
}
```

```csharp
namespace StyloIssues.Abstractions;

public sealed class StyloIssuesOptions
{
    public const string SectionName = "StyloIssues";

    public string RepoOwner { get; set; } = "";
    public string RepoName { get; set; } = "";
    public long AppId { get; set; }
    public long InstallationId { get; set; }
    public string PrivateKeyPem { get; set; } = "";   // bound from env/secret store
    public string WebhookSecret { get; set; } = "";   // bound from env/secret store
    public string MarkerKey { get; set; } = "";       // bound from env/secret store
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(2);
    public bool EnablePublicList { get; set; } = true;
    public Dictionary<string, string> CategoryLabels { get; set; } = new();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StyloIssues.Abstractions.Tests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add StyloIssues.sln src/StyloIssues.Abstractions tests/StyloIssues.Abstractions.Tests
git commit -m "feat(abstractions): DTOs, interfaces, options, reporter marker"
```

---

### Task 2: GitHub App installation-token provider

**Files:**
- Create: `src/StyloIssues/StyloIssues.csproj` (refs Abstractions, Octokit, GitHubJwt)
- Create: `src/StyloIssues/GitHub/IGitHubAppTokenProvider.cs`
- Create: `src/StyloIssues/GitHub/GitHubAppTokenProvider.cs`
- Test: `tests/StyloIssues.Tests/GitHubAppTokenProviderTests.cs`

**Interfaces:**
- Consumes: `StyloIssuesOptions` (Task 1).
- Produces: `IGitHubAppTokenProvider.GetInstallationTokenAsync(CancellationToken) : Task<string>`; caches until `TimeProvider`-now passes expiry minus a skew.

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class GitHubAppTokenProviderTests
{
    static IOptions<StyloIssuesOptions> Opts() =>
        Options.Create(new StyloIssuesOptions { AppId = 1, InstallationId = 2, PrivateKeyPem = "pem" });

    [Fact]
    public async Task Caches_token_until_near_expiry_then_refetches()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var fetches = 0;
        var sut = new GitHubAppTokenProvider(Opts(), time,
            fetch: (_, _) => { fetches++; return Task.FromResult(("tok" + fetches, time.GetUtcNow().AddHours(1))); });

        Assert.Equal("tok1", await sut.GetInstallationTokenAsync(default));
        Assert.Equal("tok1", await sut.GetInstallationTokenAsync(default)); // cached
        Assert.Equal(1, fetches);

        time.Advance(TimeSpan.FromMinutes(56));                              // within skew of 1h expiry
        Assert.Equal("tok2", await sut.GetInstallationTokenAsync(default));  // refetched
        Assert.Equal(2, fetches);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter GitHubAppTokenProviderTests`
Expected: FAIL, type does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace StyloIssues.GitHub;
public interface IGitHubAppTokenProvider
{
    Task<string> GetInstallationTokenAsync(CancellationToken ct);
}
```

```csharp
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider
{
    // Fetch delegate returns (token, absoluteExpiry). Real impl mints an App JWT
    // via GitHubJwt from options.PrivateKeyPem, then POSTs installations/{id}/access_tokens.
    public delegate Task<(string token, DateTimeOffset expiry)> Fetch(StyloIssuesOptions o, CancellationToken ct);

    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);
    private readonly StyloIssuesOptions _o;
    private readonly TimeProvider _time;
    private readonly Fetch _fetch;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiry = DateTimeOffset.MinValue;

    public GitHubAppTokenProvider(IOptions<StyloIssuesOptions> o, TimeProvider time, Fetch fetch)
    { _o = o.Value; _time = time; _fetch = fetch; }

    public async Task<string> GetInstallationTokenAsync(CancellationToken ct)
    {
        if (_token is not null && _time.GetUtcNow() < _expiry - Skew) return _token;
        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && _time.GetUtcNow() < _expiry - Skew) return _token;
            (_token, _expiry) = await _fetch(_o, ct);
            return _token!;
        }
        finally { _gate.Release(); }
    }
}
```

Add `Microsoft.Extensions.TimeProvider.Testing` to the test project.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/StyloIssues.Tests --filter GitHubAppTokenProviderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(github): cached App installation-token provider"
```

---

### Task 3: Issue gateway create-path (footer, marker, category label)

**Files:**
- Create: `src/StyloIssues/GitHub/OctokitIssueGateway.cs`
- Create: `src/StyloIssues/GitHub/AttributionFooter.cs`
- Test: `tests/StyloIssues.Tests/AttributionFooterTests.cs`

**Interfaces:**
- Consumes: `IGitHubAppTokenProvider` (Task 2), `StyloIssuesOptions`, `ReporterContext`, `NewIssueRequest`, `ReporterMarker` (Task 1).
- Produces: `AttributionFooter.Build(ReporterContext) : string`; `OctokitIssueGateway` implementing `IIssueGateway` (create path used here, rest in Task 4). Category maps to label via `options.CategoryLabels`, defaulting to the raw category string when unmapped.

- [ ] **Step 1: Write the failing test**

```csharp
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class AttributionFooterTests
{
    [Fact]
    public void Build_names_display_name_and_mentions_linked_handle()
    {
        var f = AttributionFooter.Build(new ReporterContext("Ada L.", "adalovelace", "m"));
        Assert.Contains("Ada L.", f);
        Assert.Contains("@adalovelace", f);
        Assert.Contains("stylo.bot", f);
    }

    [Fact]
    public void Build_omits_mention_when_not_linked()
    {
        var f = AttributionFooter.Build(new ReporterContext("Ada L.", null, "m"));
        Assert.Contains("Ada L.", f);
        Assert.DoesNotContain("@", f);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter AttributionFooterTests`
Expected: FAIL, `AttributionFooter` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using StyloIssues.Abstractions;
namespace StyloIssues.GitHub;

public static class AttributionFooter
{
    public static string Build(ReporterContext r)
    {
        var who = r.GitHubLogin is { Length: > 0 } gh ? $"{r.DisplayName} (@{gh})" : r.DisplayName;
        return $"\n\n---\n_Filed via stylo.bot on behalf of {who}._";
    }
}
```

`OctokitIssueGateway.cs` (create path; class continues in Task 4). Octokit calls go through a token from the provider; label resolves from options:

```csharp
using Octokit;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed partial class OctokitIssueGateway : IIssueGateway
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
        return Map(issue, Array.Empty<IssueComment>());
    }

    internal static IssueDetail Map(Issue i, IReadOnlyList<IssueComment> comments) =>
        new(i.Number, i.Title, i.State.StringValue, i.UpdatedAt ?? i.CreatedAt, i.HtmlUrl, i.Body ?? "", comments);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StyloIssues.Tests --filter AttributionFooterTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(github): issue create path with attribution + marker + label"
```

---

### Task 4: Gateway read + comment + state paths

**Files:**
- Modify: `src/StyloIssues/GitHub/OctokitIssueGateway.cs` (add partial members)
- Create: `src/StyloIssues/GitHub/OctokitIssueGateway.Reads.cs`
- Test: `tests/StyloIssues.Tests/OctokitIssueGatewayTests.cs`

**Interfaces:**
- Consumes: Task 3 gateway, `IGitHubClient` (Octokit), `ReporterMarker.SearchTerm`.
- Produces: `GetIssueAsync`, `ListByReporterAsync` (GitHub search by marker term), `ListPublicAsync`, `AddCommentAsync` on `OctokitIssueGateway`.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter OctokitIssueGatewayTests`
Expected: FAIL, `ListByReporterAsync` not implemented.

- [ ] **Step 3: Write minimal implementation**

`OctokitIssueGateway.Reads.cs`:

```csharp
using Octokit;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed partial class OctokitIssueGateway
{
    public async Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var issue = await client.Issue.Get(_o.RepoOwner, _o.RepoName, number);
        if (issue is null) return null;
        var raw = await client.Issue.Comment.GetAllForIssue(_o.RepoOwner, _o.RepoName, number);
        var comments = raw.Select(c => new IssueComment(
            c.User.Login, c.Body, c.CreatedAt, c.User.Type == AccountType.Bot)).ToList();
        return Map(issue, comments);
    }

    public async Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var req = new SearchIssuesRequest($"\"{ReporterMarker.SearchTerm(marker)}\"")
        { Repos = { $"{_o.RepoOwner}/{_o.RepoName}" } };
        var res = await client.Search.SearchIssues(req);
        return res.Items.Select(i => (IssueSummary)Map(i, Array.Empty<IssueComment>())).ToList();
    }

    public async Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        var all = await client.Issue.GetAllForRepository(_o.RepoOwner, _o.RepoName,
            new RepositoryIssueRequest { State = ItemStateFilter.All });
        return all.Select(i => (IssueSummary)Map(i, Array.Empty<IssueComment>())).ToList();
    }

    public async Task AddCommentAsync(int number, string body, ReporterContext reporter, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        await client.Issue.Comment.Create(_o.RepoOwner, _o.RepoName, number, body + AttributionFooter.Build(reporter));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/StyloIssues.Tests --filter OctokitIssueGatewayTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(github): read/list-by-reporter/public/comment gateway paths"
```

---

### Task 5: Short-TTL read cache (GitHub-as-store default)

**Files:**
- Create: `src/StyloIssues/CachingIssueReader.cs`
- Create: `src/StyloIssues/IIssueReader.cs`
- Test: `tests/StyloIssues.Tests/CachingIssueReaderTests.cs`

**Interfaces:**
- Consumes: `IIssueGateway` (Tasks 3-4), `StyloIssuesOptions.CacheTtl`, `TimeProvider`.
- Produces: `IIssueReader` with `GetIssueAsync`, `ListByReporterAsync`, `ListPublicAsync`, and `Invalidate(int number)` / `InvalidateAll()`. `CachingIssueReader` serves within TTL, refetches after, and exposes invalidation for the webhook.

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using StyloIssues.Abstractions;
using StyloIssues;
using Xunit;

public class CachingIssueReaderTests
{
    [Fact]
    public async Task Serves_from_cache_within_ttl_then_refetches_after_invalidate()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var gw = new Mock<IIssueGateway>();
        var calls = 0;
        gw.Setup(g => g.GetIssueAsync(7, It.IsAny<CancellationToken>()))
          .ReturnsAsync(() => { calls++; return new IssueDetail(7, "t", "open", time.GetUtcNow(), "u", "b", []); });

        var reader = new CachingIssueReader(gw.Object,
            Options.Create(new StyloIssuesOptions { CacheTtl = TimeSpan.FromMinutes(2) }), time);

        await reader.GetIssueAsync(7, default);
        await reader.GetIssueAsync(7, default);
        Assert.Equal(1, calls);                 // second served from cache

        reader.Invalidate(7);
        await reader.GetIssueAsync(7, default);
        Assert.Equal(2, calls);                 // refetched after invalidation
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter CachingIssueReaderTests`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace StyloIssues;
using StyloIssues.Abstractions;

public interface IIssueReader
{
    Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct);
    void Invalidate(int number);
    void InvalidateAll();
}
```

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues;

/// <summary>
/// GitHub is the source of truth. This is a transient per-process read cache
/// (not a store): it never holds authoritative state, only a short-lived copy
/// the reconciler and webhook keep honest. Fine as a ConcurrentDictionary since
/// it is a performance cache, not persistence.
/// </summary>
public sealed class CachingIssueReader : IIssueReader
{
    private readonly IIssueGateway _gw;
    private readonly TimeProvider _time;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<int, (DateTimeOffset at, IssueDetail v)> _issues = new();

    public CachingIssueReader(IIssueGateway gw, IOptions<StyloIssuesOptions> o, TimeProvider time)
    { _gw = gw; _time = time; _ttl = o.Value.CacheTtl; }

    public async Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
    {
        if (_issues.TryGetValue(number, out var e) && _time.GetUtcNow() - e.at < _ttl) return e.v;
        var fresh = await _gw.GetIssueAsync(number, ct);
        if (fresh is not null) _issues[number] = (_time.GetUtcNow(), fresh);
        return fresh;
    }

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct) =>
        _gw.ListByReporterAsync(marker, ct);   // lists are not cached: freshness over locality

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct) =>
        _gw.ListPublicAsync(ct);

    public void Invalidate(int number) => _issues.TryRemove(number, out _);
    public void InvalidateAll() => _issues.Clear();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/StyloIssues.Tests --filter CachingIssueReaderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(read): short-ttl issue read cache with invalidation"
```

---

### Task 6: Webhook HMAC verification + sink

**Files:**
- Create: `src/StyloIssues/Webhook/WebhookVerifier.cs`
- Create: `src/StyloIssues/Webhook/WebhookHandler.cs`
- Test: `tests/StyloIssues.Tests/WebhookVerifierTests.cs`

**Interfaces:**
- Consumes: `StyloIssuesOptions.WebhookSecret`, `IIssueReader.Invalidate` (Task 5).
- Produces: `WebhookVerifier.IsValid(byte[] payload, string signatureHeader, string secret) : bool` (constant-time compare of `sha256=` HMAC); `WebhookHandler.HandleAsync(string eventType, JsonElement payload)` invalidates the affected issue.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Security.Cryptography;
using System.Text;
using StyloIssues.Webhook;
using Xunit;

public class WebhookVerifierTests
{
    const string Secret = "whsec-123";

    static string Sign(byte[] body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return "sha256=" + Convert.ToHexStringLower(h.ComputeHash(body));
    }

    [Fact]
    public void Accepts_a_correct_signature()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        Assert.True(WebhookVerifier.IsValid(body, Sign(body), Secret));
    }

    [Fact]
    public void Rejects_a_tampered_body()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        var sig = Sign(body);
        var tampered = Encoding.UTF8.GetBytes("{\"action\":\"closed\"}");
        Assert.False(WebhookVerifier.IsValid(tampered, sig, Secret));
    }

    [Fact]
    public void Rejects_missing_or_malformed_header()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.False(WebhookVerifier.IsValid(body, "", Secret));
        Assert.False(WebhookVerifier.IsValid(body, "garbage", Secret));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter WebhookVerifierTests`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace StyloIssues.Webhook;

public static class WebhookVerifier
{
    public static bool IsValid(byte[] payload, string signatureHeader, string secret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix)) return false;
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexStringLower(h.ComputeHash(payload));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }
}
```

```csharp
using System.Text.Json;
namespace StyloIssues.Webhook;

public sealed class WebhookHandler
{
    private readonly IIssueReader _reader;
    public WebhookHandler(IIssueReader reader) => _reader = reader;

    public Task HandleAsync(string eventType, JsonElement payload)
    {
        if (eventType is "issues" or "issue_comment"
            && payload.TryGetProperty("issue", out var issue)
            && issue.TryGetProperty("number", out var n) && n.TryGetInt32(out var number))
        {
            _reader.Invalidate(number);
        }
        return Task.CompletedTask;
    }
}
```

Add `using StyloIssues;` to `WebhookHandler.cs` for `IIssueReader`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StyloIssues.Tests --filter WebhookVerifierTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(webhook): HMAC verification + issue-invalidating sink"
```

---

### Task 7: Reconciler hosted service (webhook backstop)

**Files:**
- Create: `src/StyloIssues/Sync/ReconcilerService.cs`
- Test: `tests/StyloIssues.Tests/ReconcilerServiceTests.cs`

**Interfaces:**
- Consumes: `IIssueReader.InvalidateAll` (Task 5), `TimeProvider`.
- Produces: `ReconcilerService : BackgroundService`; on each tick calls `InvalidateAll()` so the next read pulls canonical GitHub state. Interval from options (add `ReconcileInterval`, default 10 min).

- [ ] **Step 1: Add the option**

Modify `StyloIssuesOptions.cs`: add `public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromMinutes(10);`.

- [ ] **Step 2: Write the failing test**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.Sync;
using Xunit;

public class ReconcilerServiceTests
{
    [Fact]
    public async Task Invalidates_all_on_each_interval()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var reader = new Mock<IIssueReader>();
        var sut = new ReconcilerService(reader.Object,
            Options.Create(new StyloIssuesOptions { ReconcileInterval = TimeSpan.FromMinutes(10) }), time);

        await sut.StartAsync(default);
        time.Advance(TimeSpan.FromMinutes(21));   // two ticks
        await sut.StopAsync(default);

        reader.Verify(r => r.InvalidateAll(), Times.AtLeast(2));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter ReconcilerServiceTests`
Expected: FAIL.

- [ ] **Step 4: Write minimal implementation**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.Sync;

public sealed class ReconcilerService : BackgroundService
{
    private readonly IIssueReader _reader;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;

    public ReconcilerService(IIssueReader reader, IOptions<StyloIssuesOptions> o, TimeProvider time)
    { _reader = reader; _time = time; _interval = o.Value.ReconcileInterval; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval, _time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            _reader.InvalidateAll();
    }
}
```

- [ ] **Step 5: Run test to verify it passes, then commit**

Run: `dotnet test tests/StyloIssues.Tests --filter ReconcilerServiceTests`
Expected: PASS.

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(sync): reconciler backstop invalidates read cache on interval"
```

---

### Task 8: Default form policy + DI (`AddStyloIssues`)

**Files:**
- Create: `src/StyloIssues/DefaultFeedbackFormPolicy.cs`
- Create: `src/StyloIssues/NullIssueStore.cs`
- Create: `src/StyloIssues/StyloIssuesServiceCollectionExtensions.cs`
- Test: `tests/StyloIssues.Tests/DefaultFeedbackFormPolicyTests.cs`

**Interfaces:**
- Consumes: `IFeedbackFormPolicy`, `ICurrentUser`, all services above.
- Produces: `DefaultFeedbackFormPolicy` (always `Full`); `AddStyloIssues(IServiceCollection, Action<StyloIssuesOptions>)` registering options, `TimeProvider`, token provider (with the real GitHubJwt fetch), gateway, reader, webhook handler, reconciler hosted service, and `TryAdd` defaults for `IFeedbackFormPolicy` + `IIssueStore` so hosts can override.

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.AspNetCore.Http;
using StyloIssues;
using StyloIssues.Abstractions;
using Moq;
using Xunit;

public class DefaultFeedbackFormPolicyTests
{
    [Fact]
    public void Default_policy_is_full_form()
    {
        var user = new Mock<ICurrentUser>().Object;
        var view = new DefaultFeedbackFormPolicy().Evaluate(new DefaultHttpContext(), user);
        Assert.Equal(FeedbackFormState.Full, view.State);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter DefaultFeedbackFormPolicyTests`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Microsoft.AspNetCore.Http;
using StyloIssues.Abstractions;
namespace StyloIssues;

public sealed class DefaultFeedbackFormPolicy : IFeedbackFormPolicy
{
    public FeedbackVerdictView Evaluate(HttpContext context, ICurrentUser user) =>
        new(FeedbackFormState.Full, null, null, 0, "none", null);
}
```

```csharp
using StyloIssues.Abstractions;
namespace StyloIssues;

public sealed class NullIssueStore : IIssueStore
{
    public Task UpsertAsync(IssueDetail issue, string? reporterMarker, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IssueSummary>>([]);
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using StyloIssues.Sync;
using StyloIssues.Webhook;

namespace StyloIssues;

public static class StyloIssuesServiceCollectionExtensions
{
    public static IServiceCollection AddStyloIssues(this IServiceCollection services, Action<StyloIssuesOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IGitHubAppTokenProvider>(sp => new GitHubAppTokenProvider(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StyloIssuesOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            GitHubAppAuth.FetchInstallationToken));   // real GitHubJwt-backed fetch

        services.AddSingleton<Func<string, IGitHubClient>>(_ => token =>
            new GitHubClient(new ProductHeaderValue("styloissues")) { Credentials = new Credentials(token) });

        services.AddSingleton<IIssueGateway, OctokitIssueGateway>();
        services.AddSingleton<IIssueReader, CachingIssueReader>();
        services.AddSingleton<WebhookHandler>();
        services.AddHostedService<ReconcilerService>();

        services.TryAddSingleton<IFeedbackFormPolicy, DefaultFeedbackFormPolicy>();
        services.TryAddSingleton<IIssueStore, NullIssueStore>();
        return services;
    }
}
```

Create `src/StyloIssues/GitHub/GitHubAppAuth.cs` with `FetchInstallationToken(StyloIssuesOptions o, CancellationToken ct)` that mints the App JWT via GitHubJwt from `o.PrivateKeyPem` and calls `client.GitHubApps.CreateInstallationToken(o.InstallationId)`, returning `(token.Token, token.ExpiresAt)`.

- [ ] **Step 4: Run test to verify it passes, then commit**

Run: `dotnet test tests/StyloIssues.Tests --filter DefaultFeedbackFormPolicyTests`
Expected: PASS.

```bash
git add src/StyloIssues tests/StyloIssues.Tests
git commit -m "feat(di): default form policy, null store, AddStyloIssues wiring"
```

---

### Task 9: UI (Razor Class Library endpoints + partials, HTMX/Alpine, degrade-well)

**Files:**
- Create: `src/StyloIssues.Ui/StyloIssues.Ui.csproj` (Razor SDK, refs StyloIssues)
- Create: `src/StyloIssues.Ui/FeedbackEndpoints.cs` (maps routes)
- Create: `src/StyloIssues.Ui/Pages/_List.cshtml`, `_New.cshtml`, `_Detail.cshtml`, `_Comment.cshtml`, `_BareVerdict.cshtml`
- Create: `src/StyloIssues.Ui/wwwroot/htmx.min.js`, `wwwroot/alpine.min.js` (vendored)
- Test: `tests/StyloIssues.Ui.Tests/FeedbackEndpointsTests.cs` (WebApplicationFactory)

**Interfaces:**
- Consumes: `IIssueReader`, `IIssueGateway`, `ICurrentUser`, `IFeedbackFormPolicy`, `WebhookHandler`, `WebhookVerifier`.
- Produces: `MapStyloIssues(this IEndpointRouteConventionBuilder)` mapping `GET /feedback`, `GET|POST /feedback/new`, `GET /feedback/{number:int}`, `POST /feedback/{number:int}/comment`, `POST /feedback/webhook`. `POST /feedback/new` re-evaluates the form policy server-side and returns 403 when `State == Bare`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class FeedbackEndpointsTests : IClassFixture<WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup>>
{
    private readonly WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup> _f;
    public FeedbackEndpointsTests(WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup> f) => _f = f;

    [Fact]
    public async Task Post_new_is_refused_when_policy_is_bare()
    {
        // TestStartup binds a stub IFeedbackFormPolicy returning Bare and a stub ICurrentUser (authenticated).
        var client = _f.CreateClient();
        var resp = await client.PostAsync("/feedback/new",
            new FormUrlEncodedContent(new Dictionary<string, string>
            { ["title"] = "x", ["body"] = "y", ["category"] = "bug" }));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Ui.Tests`
Expected: FAIL (endpoint not mapped).

- [ ] **Step 3: Write minimal implementation**

`FeedbackEndpoints.cs` (server-side gate is the real control; the Bare view just hides the button):

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StyloIssues;
using StyloIssues.Abstractions;

namespace StyloIssues.Ui;

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapStyloIssues(this IEndpointRouteBuilder app)
    {
        app.MapGet("/feedback", RenderList);
        app.MapGet("/feedback/new", RenderNew);
        app.MapPost("/feedback/new", CreateIssue);
        app.MapGet("/feedback/{number:int}", RenderDetail);
        app.MapPost("/feedback/{number:int}/comment", AddComment);
        app.MapPost("/feedback/webhook", Webhook);
        return app;
    }

    private static async Task<IResult> CreateIssue(HttpContext ctx, IFeedbackFormPolicy policy,
        ICurrentUser user, IIssueGateway gw, Microsoft.Extensions.Options.IOptions<StyloIssuesOptions> opts,
        [Microsoft.AspNetCore.Mvc.FromForm] string title,
        [Microsoft.AspNetCore.Mvc.FromForm] string body,
        [Microsoft.AspNetCore.Mvc.FromForm] string category)
    {
        if (!user.IsAuthenticated || user.StableId is null) return Results.Challenge();
        // Server-side gate: never trust the hidden button. Refuse above the floor.
        if (policy.Evaluate(ctx, user).State == FeedbackFormState.Bare) return Results.Forbid();

        var marker = ReporterMarker.Compute(
            System.Text.Encoding.UTF8.GetBytes(opts.Value.MarkerKey), user.StableId);
        var issue = await gw.CreateIssueAsync(new NewIssueRequest(title, body, category),
            new ReporterContext(user.DisplayName, user.GitHubLogin, marker), ctx.RequestAborted);
        return Results.Redirect($"/feedback/{issue.Number}");
    }

    // RenderList / RenderNew / RenderDetail / AddComment render the partials below.
    // Webhook reads the raw body, verifies via WebhookVerifier, dispatches to WebhookHandler.
}
```

`_New.cshtml` (HTMX boost + Alpine model; degrades to a plain POST form; submit hidden when Bare):

```cshtml
@model StyloIssues.Abstractions.FeedbackVerdictView
<form method="post" action="/feedback/new" hx-post="/feedback/new" hx-push-url="true"
      x-data="{ title:'', body:'' }">
    <input name="title" x-model="title" required maxlength="120" />
    <textarea name="body" x-model="body" required></textarea>
    <select name="category"><option>bug</option><option>feature</option></select>
    @if (Model.State != StyloIssues.Abstractions.FeedbackFormState.Bare)
    {
        <button type="submit" :disabled="!title || !body">Send</button>
    }
    else
    {
        <partial name="_BareVerdict" model="Model" />
    }
</form>
```

`_BareVerdict.cshtml` (the dogfood payload, transparent):

```cshtml
@model StyloIssues.Abstractions.FeedbackVerdictView
<div class="sb-verdict">
  StyloBot classified this request as @Model.BotName · @Model.BotType ·
  @($"{Model.Probability:P0}") · threat @Model.ThreatBand.
  <a href="https://github.com/scottgal/stylobot/issues">View issues on GitHub</a>
</div>
```

Provide `TestStartup` in the Ui test project that calls `AddStyloIssues`, overrides `IFeedbackFormPolicy` with a Bare stub and `ICurrentUser` with an authenticated stub, and `MapStyloIssues`.

- [ ] **Step 4: Run test to verify it passes, then commit**

Run: `dotnet test tests/StyloIssues.Ui.Tests`
Expected: PASS.

```bash
git add src/StyloIssues.Ui tests/StyloIssues.Ui.Tests
git commit -m "feat(ui): feedback endpoints + HTMX/Alpine partials + server-side gate"
```

---

### Task 10: Sample host + end-to-end smoke

**Files:**
- Create: `samples/StyloIssues.Sample/Program.cs`
- Create: `samples/StyloIssues.Sample/DemoCurrentUser.cs`
- Test: `tests/StyloIssues.Ui.Tests/ListRendersTests.cs`

**Interfaces:**
- Consumes: `AddStyloIssues`, `MapStyloIssues`, a demo `ICurrentUser`.
- Produces: a runnable sample proving zero-infra adoption (no database), and a test that `GET /feedback` renders a list from a stub `IIssueReader`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ListRendersTests : IClassFixture<WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup>>
{
    private readonly WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup> _f;
    public ListRendersTests(WebApplicationFactory<StyloIssues.Ui.Tests.TestStartup> f) => _f = f;

    [Fact]
    public async Task Get_feedback_returns_ok_and_html()
    {
        var resp = await _f.CreateClient().GetAsync("/feedback");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.ToString());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Ui.Tests --filter ListRendersTests`
Expected: FAIL (list render not complete).

- [ ] **Step 3: Implement `RenderList` + `_List.cshtml` and the sample `Program.cs`**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStyloIssues(o =>
{
    o.RepoOwner = "scottgal"; o.RepoName = "stylobot";
    o.AppId = builder.Configuration.GetValue<long>("StyloIssues:AppId");
    o.InstallationId = builder.Configuration.GetValue<long>("StyloIssues:InstallationId");
    o.PrivateKeyPem = builder.Configuration["StyloIssues:PrivateKeyPem"] ?? "";
    o.WebhookSecret = builder.Configuration["StyloIssues:WebhookSecret"] ?? "";
    o.MarkerKey = builder.Configuration["StyloIssues:MarkerKey"] ?? "";
});
builder.Services.AddSingleton<StyloIssues.Abstractions.ICurrentUser, DemoCurrentUser>();
var app = builder.Build();
app.MapStyloIssues();
app.Run();
```

`RenderList` resolves `IIssueReader` + `ICurrentUser`, computes the marker (when authenticated) for "my issues", optionally `ListPublicAsync`, and returns the `_List` partial as HTML content.

- [ ] **Step 4: Run test to verify it passes, then commit**

Run: `dotnet test tests/StyloIssues.Ui.Tests --filter ListRendersTests`
Expected: PASS.

```bash
git add samples/StyloIssues.Sample tests/StyloIssues.Ui.Tests src/StyloIssues.Ui
git commit -m "feat(sample): zero-infra sample host + list render smoke test"
```

---

### Task 11: Attachment seam (`IIssueAttachmentSource`) [stub for StyloDump]

**Files:**
- Modify: `src/StyloIssues.Abstractions/Interfaces.cs` (add interface)
- Modify: `src/StyloIssues.Abstractions/Dtos.cs` (add `IssueAttachment`)
- Create: `src/StyloIssues/NullIssueAttachmentSource.cs`
- Modify: `src/StyloIssues/StyloIssuesServiceCollectionExtensions.cs` (TryAdd default)
- Modify: `src/StyloIssues.Ui/FeedbackEndpoints.cs` (capture + inline on create)
- Test: `tests/StyloIssues.Tests/AttachmentBodyTests.cs`

**Interfaces:**
- Consumes: nothing new; stays decoupled from StyloDump (StyloIssues never
  references StyloDump; it passes primitive scope params).
- Produces: `IIssueAttachmentSource.CaptureAsync(string? fingerprint, DateTimeOffset from, DateTimeOffset to, CancellationToken) : Task<IssueAttachment?>`; `IssueAttachment(string FileName, byte[]? Bytes, string? Url, string ContentType, string? ManifestSummary)`; `AttachmentBody.Append(string body, IssueAttachment a) : string` appending a link line and a collapsed `<details>` manifest summary. Default DI binding is `NullIssueAttachmentSource` (returns null).

- [ ] **Step 1: Write the failing test**

```csharp
using StyloIssues.Abstractions;
using StyloIssues;
using Xunit;

public class AttachmentBodyTests
{
    [Fact]
    public void Append_adds_link_and_collapsed_manifest_summary()
    {
        var a = new IssueAttachment("dump.zip", null, "https://host/dumps/abc.zip",
            "application/zip", "{\"scope\":{\"fingerprint\":\"fp1\"}}");
        var body = AttachmentBody.Append("Original report.", a);

        Assert.Contains("Original report.", body);
        Assert.Contains("https://host/dumps/abc.zip", body);
        Assert.Contains("<details>", body);
        Assert.Contains("\"fingerprint\":\"fp1\"", body);
    }

    [Fact]
    public void Append_is_a_noop_when_nothing_to_add()
    {
        var a = new IssueAttachment("dump.zip", null, null, "application/zip", null);
        Assert.Equal("Original report.", AttachmentBody.Append("Original report.", a));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/StyloIssues.Tests --filter AttachmentBodyTests`
Expected: FAIL, `AttachmentBody` / `IssueAttachment` do not exist.

- [ ] **Step 3: Write minimal implementation**

Add to `Dtos.cs`:

```csharp
public sealed record IssueAttachment(
    string FileName, byte[]? Bytes, string? Url, string ContentType, string? ManifestSummary);
```

Add to `Interfaces.cs`:

```csharp
/// <summary>
/// Optional host hook to attach a diagnostic archive (e.g. StyloDump) to a new
/// issue. StyloIssues passes primitive scope params and stays unaware of how the
/// archive is produced or hosted. Default binding returns null.
/// </summary>
public interface IIssueAttachmentSource
{
    Task<IssueAttachment?> CaptureAsync(string? fingerprint, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
```

Create `AttachmentBody.cs` in `src/StyloIssues`:

```csharp
using StyloIssues.Abstractions;
namespace StyloIssues;

public static class AttachmentBody
{
    public static string Append(string body, IssueAttachment a)
    {
        var b = body;
        if (!string.IsNullOrEmpty(a.Url))
            b += $"\n\n**Diagnostic archive:** [{a.FileName}]({a.Url})";
        if (!string.IsNullOrEmpty(a.ManifestSummary))
            b += $"\n\n<details><summary>Detection snapshot manifest</summary>\n\n```json\n{a.ManifestSummary}\n```\n\n</details>";
        return b;
    }
}
```

Create `NullIssueAttachmentSource.cs`:

```csharp
using StyloIssues.Abstractions;
namespace StyloIssues;

public sealed class NullIssueAttachmentSource : IIssueAttachmentSource
{
    public Task<IssueAttachment?> CaptureAsync(string? fingerprint, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Task.FromResult<IssueAttachment?>(null);
}
```

In `AddStyloIssues` add: `services.TryAddSingleton<IIssueAttachmentSource, NullIssueAttachmentSource>();`

In `CreateIssue` (Task 9), after computing the marker and before creating, when a form field `attach == "on"` and the source returns non-null, fold it into the body:

```csharp
// resolved via DI: IIssueAttachmentSource attachments; form field: string? attach
var reqBody = body;
if (attach == "on")
{
    var now = /* TimeProvider */ DateTimeOffset.UtcNow;
    var att = await attachments.CaptureAsync(
        ctx.Request.Headers["X-SB-Fingerprint"].FirstOrDefault(),
        now.AddHours(-1), now, ctx.RequestAborted);
    if (att is not null) reqBody = AttachmentBody.Append(reqBody, att);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StyloIssues.Tests --filter AttachmentBodyTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/StyloIssues.Abstractions src/StyloIssues src/StyloIssues.Ui tests/StyloIssues.Tests
git commit -m "feat(attach): IIssueAttachmentSource seam + inline manifest (StyloDump stub)"
```

---

## Deferred companion plans

- **Plan A: StyloDump core** (new repo/package). `DumpScope`,
  `IDiagnosticContributor`, `IDumpArchive`, `IDumpService`, zip assembly, the
  schema-versioned `manifest.json`. Collection internals; no StyloBot coupling.
- **Plan B: StyloBot diagnostic contributor** (FOSS `stylobot` repo,
  `Mostlylucid.BotDetection.StyloDump`). Implements `IDiagnosticContributor` by
  tapping the ephemeral signal sinks for the scoped fingerprint/endpoint/window.
- **Plan 2: StyloBot verdict bridge** (form-policy band mapping + PoW
  challenge-to-unlock); waits on the foss dogfood-seam answer.
- **Plan 3: site wiring** (Keycloak `ICurrentUser`, GitHub linking + realm
  hardening, Postgres `IIssueStore`, StyloDump-backed `IIssueAttachmentSource`).

## Self-Review

**Spec coverage:** package split (Task 1); GitHub-App gateway + Octokit (Tasks 2-4); GitHub-as-store default + short cache (Task 5); webhook + reconciler (Tasks 6-7); pluggable form policy + optional store + DI (Task 8); SSR + HTMX + Alpine UI, server-side gate, bare-verdict, degrade-well (Task 9); zero-infra adoption proof (Task 10). Reporter marker / zero-PII (Task 1, used in 3/9). Attribution + App-posts-on-behalf (Task 3). Verdict-adaptive bands: the *default* policy (Task 8) returns Full; the actual band mapping lives in the **bridge (plan 2)**, which this package only exposes a seam for, matching the spec's layer split.

**Deferred to later plans (by design):** GitHub linking / Keycloak `ICurrentUser` / realm hardening (site, plan 3); verdict band mapping + PoW challenge-to-unlock (bridge, plan 2); Postgres `IIssueStore` (site, plan 3).

**Placeholder scan:** endpoints for `RenderList/RenderNew/RenderDetail/AddComment/Webhook` are described with their responsibilities; `RenderList` gets full treatment in Task 10 as the smoke-tested path. Before executing Task 9, expand the remaining render bodies from the `_Detail`/`_Comment` partial shapes shown. No TBDs in logic-bearing tasks.

**Type consistency:** `IIssueReader`, `IIssueGateway`, `ReporterContext`, `FeedbackFormState`, `FeedbackVerdictView`, `ReporterMarker.Compute/Embed/SearchTerm`, `GetInstallationTokenAsync`, `Invalidate/InvalidateAll` are used consistently across tasks.
