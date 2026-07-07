# StyloIssues + StyloBot Feedback Bridge (Design Spec)

**Date:** 2026-07-07
**Status:** Design approved, pre-plan
**Author:** Feedback / StyloIssues feature agent

## 1. Summary

A feedback feature for the StyloBot site: users file bug reports and feature
requests through a first-class UX on the site, and those flow two-way to and
from GitHub issues on `scottgal/stylobot`. The site never becomes a second
issue tracker; GitHub stays the source of truth and gets the full "fix it"
workflow (labels, milestones, PR links, CI). The site provides a nicer
front door and, crucially, a place to dogfood StyloBot: the feedback form
adapts to the visitor's live bot verdict.

The work splits into three layers across repos:

1. **StyloIssues** (new standalone repo + NuGet package family): a reusable,
   framework-agnostic "GitHub issues as a feature in your ASP.NET site"
   library. SSR + HTMX + Alpine UX, two-way GitHub sync, pluggable identity
   and a pluggable form-policy hook. No StyloBot coupling.
2. **StyloBot verdict bridge** (`Mostlylucid.BotDetection.StyloIssues`, in the
   FOSS `stylobot` repo): a small package that implements StyloIssues'
   form-policy hook from the live detection verdict, so the form dogfoods
   StyloBot. StyloBot *core* takes no dependency on StyloIssues.
3. **Site wiring** (in `stylobot-commercial` website): references StyloIssues
   plus the bridge, supplies the Keycloak `ICurrentUser`, mounts the UI, and
   holds the Keycloak/GitHub-App secrets.

## 2. Goals and non-goals

**Goals**

- File / view / comment on GitHub issues from the site without a GitHub
  account (email-primary Keycloak login is the floor).
- Two-way sync: site actions reach GitHub; GitHub changes reach the site.
- Optional GitHub account linking for attribution and notifications only.
- The form dogfoods StyloBot: the visitor's bot verdict changes what renders.
- StyloIssues is genuinely reusable: zero StyloBot coupling, zero-infra
  default (no database required to adopt).
- SPA-quality UX that degrades gracefully to plain HTML.

**Non-goals**

- Not a replacement issue tracker. GitHub remains canonical.
- No sign-in-with-GitHub as a primary identity provider. No Google/Microsoft.
- No storage of long-lived personal GitHub tokens. The App does all posting.
- No honeypot/deception on this surface (transparency is the demo).

## 3. Topology

```
StyloDump repo (new, small reusable NuGet)          [collection internals stubbed for now]
└── StyloDump   DumpScope, IDiagnosticContributor (per-app hook), IDumpArchive,
                IDumpService -> portable zip + machine-readable manifest.json

StyloIssues repo (new, reusable NuGet)
├── StyloIssues.Abstractions   ICurrentUser, IIssueGateway, IFeedbackFormPolicy,
│                              IIssueStore (optional), IIssueAttachmentSource (optional),
│                              DTOs, options
├── StyloIssues                Octokit GitHub-App gateway, sync, SSR+HTMX+Alpine UI,
│                              AddStyloIssues() / MapStyloIssues()
└── (samples/tests)

stylobot repo (FOSS)
├── Mostlylucid.BotDetection.StyloIssues   verdict -> IFeedbackFormPolicy bridge
│                                          (refs StyloIssues.Abstractions + BotDetection core)
└── Mostlylucid.BotDetection.StyloDump     IDiagnosticContributor: dumps detection signals
                                           by fingerprint/endpoint/window via ephemeral sink hooks

stylobot-commercial (website)
└── Stylobot.Website   refs StyloIssues + StyloDump + both bridges; Keycloak ICurrentUser;
                       optional Postgres IIssueStore; StyloDump-backed IIssueAttachmentSource;
                       GitHub-App + realm secrets
```

Dependency direction stays clean: StyloBot core has no knowledge of
StyloIssues; the bridge depends on `StyloIssues.Abstractions` (small surface)
and StyloBot's public verdict API; the site depends on both.

## 4. StyloIssues package (reusable core)

### 4.1 Abstractions

- `ICurrentUser` — host supplies it. Exposes a stable opaque user id (the
  reporter identity), a display name, and an optional linked GitHub login.
  The site implementation projects these from the Keycloak principal.
- `IIssueGateway` — the GitHub side. Create issue, add comment, get issue,
  list issues (by reporter marker or label/state filter), open/close. Backed
  by Octokit with GitHub-App installation-token auth.
- `IFeedbackFormPolicy` — the extension point the bridge implements. Given the
  current request/user context, returns a `FeedbackFormState` (see 6). Default
  implementation in the package returns `Full` (no adaptation) so the package
  works standalone.
- `IIssueStore` — **optional** local read-model. Default binding is a no-op /
  GitHub-backed pass-through; hosts that want speed or rich queries bind a real
  store (the site uses Postgres). Never required to adopt the package.
- `IIssueAttachmentSource` — **optional** attachment hook (see section 15).
  Default binding returns null (no attachment). A host binds a StyloDump-backed
  implementation so an opt-in "attach my detection snapshot" captures a
  diagnostic archive and links it into the issue. StyloIssues stays unaware of
  how the archive is produced.
- DTOs: `IssueSummary`, `IssueDetail`, `IssueComment`, `NewIssueRequest`,
  `FeedbackFormState`, `FeedbackVerdictView`.
- `StyloIssuesOptions`: repo slug, GitHub App id / installation id / private
  key source, webhook secret source, reporter-marker HMAC key source, cache
  TTL, category-to-label map, public-list toggle.

### 4.2 GitHub gateway (Octokit + GitHub App)

- Auth: GitHub App installation token. App JWT minted from the private key
  (via `GitHubJwt`), exchanged for a short-lived installation token, cached
  until near expiry. Rationale for Octokit over hand-rolled HttpClient: issue
  create/comment/list plus App-token exchange is enough surface that the typed
  client earns its dependency (unlike the single anonymous GET in
  `GitHubReleasesService`).
- All writes authored by the App identity, with an attribution footer:
  `Filed via stylo.bot on behalf of {display name}` and, when linked,
  `(@{github_login})` so GitHub notifies the user.
- A hidden reporter marker is embedded in the issue body (see 7.3) so
  "my issues" can be resolved from GitHub search with no local database.

### 4.3 Store model: GitHub as source of truth

- **Default (zero-infra):** no database. Reads render from the GitHub API
  behind a short TTL in-memory cache (pattern mirrors `GitHubReleasesService`,
  ~1-5 min). "My issues" = GitHub search for the reporter marker. Adopting the
  package requires only a GitHub App, no schema, no migrations.
- **Optional read-model (`IIssueStore`):** the site binds a Postgres store for
  faster lists and richer filtering. This is a cache/read-model, not an
  authority: the reconciler (4.4) rewrites it from GitHub on a cadence, so it
  never diverges. Per the commercial one-database rule, when Postgres is wired
  no SQLite store co-registers; the default no-DB mode is the only other option.

### 4.4 Two-way sync

- **Outbound:** site action -> `IIssueGateway` -> GitHub (create issue,
  comment, open/close). Immediate.
- **Inbound (fast path):** a GitHub App webhook endpoint (`issues`,
  `issue_comment`) verified by HMAC `X-Hub-Signature-256`. On a valid event it
  invalidates the relevant cache entry (default mode) or upserts the read-model
  (store mode).
- **Inbound (backstop):** a periodic reconciler re-queries GitHub for canonical
  state on a cadence and refreshes cache/store. This catches missed or delayed
  webhooks. Compute-from-source, never assume the webhook was reliable.

### 4.5 UI (SSR + HTMX + Alpine)

- Razor views/partials shipped in the package (Razor Class Library) so the host
  maps them with one call. Endpoints:
  - `GET /feedback` — list (my issues, plus a public list if enabled).
  - `GET /feedback/new` — the file-a-report form (auth-gated by the host).
  - `POST /feedback/new` — create; re-checks the form policy server-side.
  - `GET /feedback/{ref}` — detail: threaded comments, status badge,
    "view on GitHub", link-GitHub CTA when unlinked.
  - `POST /feedback/{ref}/comment` — add a comment.
  - `POST /feedback/webhook` — GitHub App webhook sink (HMAC-verified).
- See 8 for the SPA-degrades-well behaviour.

### 4.6 DI surface

- `services.AddStyloIssues(options)` registers the gateway, sync services
  (webhook handler + hosted reconciler), and the default form policy.
- `app.MapStyloIssues()` maps the endpoints and the RCL UI.
- Hosts override `ICurrentUser`, `IFeedbackFormPolicy`, and optionally
  `IIssueStore` via normal DI replacement.

## 5. StyloBot verdict bridge (FOSS `stylobot` repo)

Package `Mostlylucid.BotDetection.StyloIssues`. Single responsibility:
implement `IFeedbackFormPolicy` by reading the live StyloBot verdict from
`HttpContext` (`IsBot()`, `GetBotConfidence()`, `GetBotType()`, bot probability,
threat score) and mapping it to a `FeedbackFormState` + `FeedbackVerdictView`.

- References only `StyloIssues.Abstractions` and StyloBot's public verdict API.
  StyloBot core stays unaware of StyloIssues.
- Lives in FOSS because it showcases detection; any StyloBot user who also uses
  StyloIssues gets the dogfooding form for free by adding this one package.
- `services.AddStyloBotFeedbackBridge()` replaces the default form policy.

## 6. Verdict-adaptive form (the dogfood)

The form renders differently by band. Band derives from the canonical
`bot_probability >= Classification.BotFloor` (never a separately stored
boolean) plus `threat_score`.

| Band | Trigger | Renders |
|------|---------|---------|
| **Human** | below floor, or `BotType.Internal` (LAN) | full chrome, full form, submit enabled |
| **Suspicious** | near floor, or elevated threat, inconclusive | form visible, submit gated behind a proof-of-work challenge (reuses existing PoW) |
| **Bot** | clearly above floor | bare layout (no nav/header/footer/marketing), submit hidden, form collapsed to a read-only shell; the verdict is rendered as the demo payload: `StyloBot classified this request as {BotName} · {BotType} · {probability}% · threat {band}`, plus a read-only "view issues on GitHub" link |

**Design rules**

- **Server enforcement is the real gate.** The `POST /feedback/new` action
  re-evaluates the form policy server-side and refuses above the floor. Hiding
  the submit button is presentation only; it never stands alone (a bot ignores
  hidden DOM and posts anyway). This is layered security, not a redundant
  second copy of one mechanism.
- **A misclassified human is never locked out.** The Suspicious/Bot paths
  always offer the PoW challenge-to-unlock and the "file directly on GitHub"
  link, so a false positive on a *feedback* form is never a dead end.
- **Transparency over honeypot.** Bots get the honest verdict, not a fake
  success trap. The holodeck stays for real attack paths; this public form is a
  showcase, so deceiving a possibly-misclassified user would be wrong.

## 7. Identity and auth

### 7.1 Login

- Email-primary Keycloak login is required to file or comment. Anonymous
  visitors can view the public list (if enabled) and are challenged on write.
- The site supplies `ICurrentUser` from the Keycloak principal (`sub`, display
  name, and linked GitHub login claim when present).

### 7.2 Optional GitHub linking

- GitHub is registered in the realm as a **profile-level link only**
  (link-existing-only first-broker-login flow), NOT a "Sign in with GitHub"
  button on the login page. Linking is opt-in, per-feature, revocable.
- Scope is `read:user` only. Not `user:email` (the App @mention already
  notifies the linked user, so email is redundant scope we would have to
  defend). The user never grants `repo`; the App does all posting.
- Standing up linking requires a real GitHub OAuth app for the Keycloak broker,
  replacing the `${KC_GITHUB_CLIENT_ID:dev-...}` placeholders. This realm
  hardening lands as its own commit, separate from the StyloIssues drop.

### 7.3 Reporter marker (zero-PII attribution)

- The reporter identity stored in / searched from GitHub is an opaque
  `HMAC-SHA256(marker_key, keycloak_sub)`, embedded as a hidden marker in the
  issue body. This lets "my issues" resolve via GitHub search with no local
  PII and no database. Consistent with the existing `Member` HMAC pattern and
  the zero-PII architecture.
- Only the public GitHub handle (when linked) and this opaque marker are
  persisted anywhere. No email, no tokens on our side.

## 8. UI: a nice SPA that degrades well

- **SPA feel via HTMX boosting.** `hx-boost` + `hx-push-url` give client-side
  navigation, partial swaps (submit -> new issue row, load comments inline,
  refresh issue state), and history without full reloads. Alpine holds the
  local reactive model (field state, character counts, optimistic disable, the
  verdict-band reveal).
- **Degrades well on two axes**, both collapsing onto the same SSR baseline:
  - *Capability:* JS off -> plain server-rendered forms and links that still
    file and comment via normal POSTs. No dead buttons.
  - *Verdict:* bot band -> the stripped bare view (which is also what a
    no-JS/low-capability client tends to get), so the degradation path and the
    dogfood path reuse one code path.
- **Vendored assets, no CDN** (HTMX + Alpine bundled), matching the dashboard's
  vendored-Chart.js approach.

## 9. Data flows

- **File an issue:** authed user submits `POST /feedback/new` -> server
  re-checks form policy -> `IIssueGateway.CreateIssue` (App token, attribution
  footer, hidden reporter marker, category->label) -> HTMX swaps the new row /
  redirects to detail. No-JS: normal POST-redirect-get.
- **Comment:** `POST /feedback/{ref}/comment` -> `IIssueGateway.AddComment`
  (App, on-behalf footer) -> HTMX appends the comment partial.
- **Inbound update:** GitHub -> `POST /feedback/webhook` (HMAC-verified) ->
  cache invalidate / store upsert -> next render reflects it.
- **Reconcile:** hosted service pulls issues updated since a cursor -> refresh
  cache/store.
- **Render form:** `IFeedbackFormPolicy` (bridge) reads verdict -> chooses band
  -> view renders full / challenge-gated / bare-with-verdict.

## 10. Security

- Webhook HMAC verification (`X-Hub-Signature-256`) with a secret from env /
  secret store; reject unsigned or mismatched payloads.
- All GitHub secrets (App private key, webhook secret, OAuth client secret,
  reporter-marker HMAC key) come from env / secret store, never config-file
  defaults.
- Server-side form-policy enforcement on every write (6).
- Body sanitisation on outbound issue/comment content; strip PII query params
  from any attached diagnostics; the "attach my detection snapshot" option is
  explicit opt-in.
- Rate limiting on write endpoints in addition to the verdict gate.

## 11. Testing

- **StyloIssues:** gateway against a recorded GitHub API / a fake
  `IIssueGateway`; webhook HMAC verification (valid/invalid/replay); reconciler
  refresh; form-policy default; reporter-marker round-trip; no-JS POST paths.
- **Bridge:** verdict -> band mapping across Human/Suspicious/Bot/Internal;
  server-side refusal above floor; PoW unlock path; false-positive escape hatch.
- **Site:** Keycloak `ICurrentUser` projection; GitHub link claim surfacing;
  Postgres `IIssueStore` upsert/read; end-to-end file-and-see-on-GitHub (staged
  against a scratch repo).

## 12. Repo and packaging layout

- New repo `styloissues`: `StyloIssues.Abstractions`, `StyloIssues`, samples,
  tests. Published as NuGet.
- `stylobot` repo: add `src/Mostlylucid.BotDetection.StyloIssues` (bridge) +
  tests. Local project reference to `StyloIssues.Abstractions` during dev
  (sibling checkout), NuGet ref for release.
- `stylobot-commercial`: website references the two packages, adds the Keycloak
  `ICurrentUser`, optional Postgres `IIssueStore`, secrets, and the realm
  hardening commit.

## 13. Open questions and follow-ups

- **foss dogfood seam (pending):** confirm the exact seam for reading the
  verdict on an authenticated POST (off `HttpContext` vs a dedicated call),
  transport `protocol_class` for a document POST, and PoW/challenge interplay.
  Asked on the agent channel (`foss-styloissues-dogfood-and-placement`); fold
  the answer in. Non-blocking; the bridge reads `HttpContext` by default.
- **overview spec review (offered):** send `feature-styloissues-spec-review`
  for a pass on the concrete `IIssueStore` shape and webhook secret rotation.
- **Realm GitHub-IdP hardening:** its own commit (link-existing-only, real
  OAuth app), separate from the StyloIssues drop.

## 14. Out of scope / future

- Reactions, assignees, milestones editing from the site (view-only for now).
- Discussions (vs issues) backing.
- Notifications digest on the site (rely on GitHub notifications via linkage).
- A second host adopting StyloIssues (proves the reusability; not required now).
- StyloDump collection internals and the StyloBot diagnostic contributor
  (section 15) are stubbed here: interfaces defined, implementations deferred.
- **Debug agents** consuming the StyloDump archive to triage issues. This is
  why the archive manifest is structured and schema-versioned from the start,
  even though collection is stubbed. Not built now.

## 15. Corollary package: StyloDump (diagnostic archive) [stubbed]

A small, standalone, reusable NuGet embeddable in any ASP.NET app. It produces
a portable diagnostic archive scoped by fingerprint and/or endpoint over a time
window, assembled from per-app extension hooks. StyloBot is one such host; the
package itself knows nothing about StyloBot.

### 15.1 Abstractions

- `DumpScope(string? Fingerprint, string? Endpoint, DateTimeOffset From, DateTimeOffset To)`
  — what to dump and for when. Any field may be null (dump-everything-for-window).
- `IDiagnosticContributor` — the per-app hook. `string Name { get; }` and
  `Task ContributeAsync(DumpScope scope, IDumpArchive archive, CancellationToken)`.
  A host registers as many as it likes; each writes named entries into the archive.
- `IDumpArchive` — write surface for a contributor: `Stream CreateEntry(string path)`
  plus `AddManifestEntry(string contributor, string path, object metadata)`.
- `IDumpService` — `Task<DumpResult> CreateDumpAsync(DumpScope scope, CancellationToken)`.
  Fans out to all registered contributors, assembles a zip, writes
  `manifest.json` at the root, returns the stream plus a summary.
- `DumpResult(Stream Archive, string ContentType, DumpManifest Manifest)`.

### 15.2 Manifest (agent-consumable)

`manifest.json` at the archive root: schema version, the `DumpScope`, produced-at
timestamp, and an index of `{ contributor, path, metadata }` entries. Structured
and versioned so a future debug agent can parse and reason over the archive
without unzipping blindly. This is the load-bearing forward-compatibility
decision; the collection internals behind it are stubbed.

### 15.3 StyloBot as a contributor (FOSS bridge, stubbed)

`Mostlylucid.BotDetection.StyloDump` implements `IDiagnosticContributor` and
dumps detection signals for the scoped fingerprint/endpoint/window by tapping
the ephemeral signal system's existing sink hooks (session vectors, signature
reputation, recent contributions). This lets a self-hosted StyloBot instance
emit an archive. StyloBot core is unaware of StyloDump; only this bridge depends
on `StyloDump`. Stubbed: interface + registration now, signal-tap fill-in later.

### 15.4 StyloIssues consumption

StyloIssues declares `IIssueAttachmentSource` (section 4.1). The site binds a
StyloDump-backed implementation. On an opt-in "attach my detection snapshot"
issue submission, StyloIssues calls the source with a `DumpScope` for the
current fingerprint and recent window, receives an `IssueAttachment`
(filename + bytes-or-hosted-URL + a short manifest summary), links it into the
issue, and drops the manifest summary inline in a collapsed `<details>` block so
it is readable (by humans and future agents) without downloading. The upload/
hosting mechanism (App-created gist vs pre-hosted object-store URL) is stubbed;
the seam is defined so the mechanism can be chosen later without touching
StyloIssues' core.

### 15.5 Boundaries

- StyloDump is generic: no StyloBot, no GitHub, no StyloIssues dependency.
- StyloIssues depends only on the `IIssueAttachmentSource` interface, not on
  StyloDump. A host without StyloDump simply gets no attachment.
- The FOSS StyloBot contributor depends on `StyloDump` + StyloBot core; core
  depends on neither.
