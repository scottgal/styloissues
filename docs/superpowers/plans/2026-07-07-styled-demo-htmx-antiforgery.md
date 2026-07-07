# Styled Demo + Htmx.Net + Antiforgery Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the StyloIssues sample into a polished HTMX+Alpine SPA-like demo with a "drafting-table editorial" aesthetic, remove redundant per-form antiforgery hx-headers from the package partials (leaving a single global token source in the layout), fix the stale README security note, and add issue number spans to the list and detail partials.

**Architecture:** Part A strips the redundant `hx-headers` token injection from the two package partials (FeedbackForm and FeedbackDetail), keeping `@Html.AntiForgeryToken()` as the single token source per form. Part B replaces the stale README security section with accurate text. Part C adds the `Htmx` NuGet package to the sample, creates a full editorial design system in `wwwroot/css/site.css`, creates `Pages/Shared/_Layout.cshtml` (with the global antiforgery `htmx:configRequest` script and a single `@Html.AntiForgeryToken()`), creates `Pages/_ViewStart.cshtml`, and updates both Razor Pages to use the layout.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, HTMX (vendored from _content/StyloIssues.UI/htmx.min.js), Alpine.js (vendored), Htmx.Net NuGet package (Htmx), Google Fonts (Fraunces / Hanken Grotesk / Space Mono), CSS custom properties.

## Global Constraints

- net10.0 target framework
- No em dashes anywhere (code, cshtml, css, comments, docs); use colons, semicolons, commas, or parentheses
- No CDN for htmx or alpine; load from `~/_content/StyloIssues.UI/htmx.min.js` and alpine.min.js
- Google Fonts link is acceptable in the demo layout
- Do not weaken antiforgery enforcement; `ValidateRequestAsync` stays in both handlers
- Keep `@Html.AntiForgeryToken()` hidden field in each package partial form for no-JS fallback
- Remove per-form `hx-headers` token injection from package partials; the global `htmx:configRequest` script in the layout is the single htmx token source
- All 24 tests must stay green after changes
- No new files unless required; prefer editing existing files

---

## File Map

**Modify (package):**
- `src/StyloIssues.UI/Views/Shared/Components/FeedbackForm/Default.cshtml` -- remove injects + tok + hx-headers; keep @Html.AntiForgeryToken()
- `src/StyloIssues.UI/Views/Shared/Components/FeedbackDetail/Default.cshtml` -- same; also add #number span before h2
- `src/StyloIssues.UI/Views/Shared/Components/FeedbackList/Default.cshtml` -- add #number span as first child of each li

**Modify (docs):**
- `README.md` -- replace stale Security notes section

**Modify/Create (sample):**
- `samples/StyloIssues.Sample/StyloIssues.Sample.csproj` -- add Htmx PackageReference
- `samples/StyloIssues.Sample/wwwroot/css/site.css` -- CREATE: full editorial design system
- `samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml` -- CREATE: sticky header, fonts, htmx+alpine load, global antiforgery script
- `samples/StyloIssues.Sample/Pages/_ViewStart.cshtml` -- CREATE: sets Layout = "_Layout"
- `samples/StyloIssues.Sample/Pages/Feedback.cshtml` -- replace bare HTML with layout-based page
- `samples/StyloIssues.Sample/Pages/FeedbackDetail.cshtml` -- replace bare HTML with layout-based page

**Append:**
- `.superpowers/sdd/final-review-fixes.md` -- append "## Styled demo + Htmx.Net + antiforgery cleanup" section

---

### Task 1: Part A - Strip redundant antiforgery from package partials; add issue number spans

**Files:**
- Modify: `src/StyloIssues.UI/Views/Shared/Components/FeedbackForm/Default.cshtml`
- Modify: `src/StyloIssues.UI/Views/Shared/Components/FeedbackDetail/Default.cshtml`
- Modify: `src/StyloIssues.UI/Views/Shared/Components/FeedbackList/Default.cshtml`

**Interfaces:**
- Consumes: nothing from other tasks
- Produces: package partials that emit `@Html.AntiForgeryToken()` hidden fields but no per-form `hx-headers`; FeedbackList emits `<span class="sb-issue-number">#N</span>` as first child of each `<li>`; FeedbackDetail header has `<span class="sb-issue-number">#N</span>` before `<h2>`

- [ ] **Step 1: Edit FeedbackForm/Default.cshtml**

Remove lines 3-4 (`@inject IAntiforgery Af` and `@inject IHttpContextAccessor Hca`).
Remove line 33 (`var tok = Af.GetAndStoreTokens(...).RequestToken;`).
Remove the `hx-headers='{"RequestVerificationToken": "@tok"}'` attribute from the `<form>` element.
Keep `@Html.AntiForgeryToken()` on line 41 (it becomes the hidden-field token source for no-JS forms and the value the global script reads for htmx requests).
After edits the file top should read:
```cshtml
@using StyloIssues.Abstractions
@using StyloIssues.UI.Models
@model FeedbackFormViewModel
@{
    Layout = null;
}
```
And the form tag should be:
```cshtml
<form method="post" action="/feedback/new"
      hx-post="/feedback/new"
      hx-target="this"
      hx-swap="outerHTML"
      x-data="{ submitting: false }"
      x-on:submit="submitting = true">
    @Html.AntiForgeryToken()
```

- [ ] **Step 2: Edit FeedbackDetail/Default.cshtml**

Remove lines 2-3 (`@inject IAntiforgery Af` and `@inject IHttpContextAccessor Hca`).
Remove `var tok = Af.GetAndStoreTokens(Hca.HttpContext!).RequestToken;`.
Remove `hx-headers='{"RequestVerificationToken": "@tok"}'` from the comment form.
Keep `@Html.AntiForgeryToken()` in the comment form.
Add `<span class="sb-issue-number">#@Model.Number</span>` as the first element inside `<header class="sb-issue-header">` before the `<h2>`.
After edits the header block should read:
```cshtml
<header class="sb-issue-header">
    <span class="sb-issue-number">#@Model.Number</span>
    <h2>@Model.Title</h2>
    ...
</header>
```
And the comment form tag:
```cshtml
<form method="post" action="/feedback/@Model.Number/comment"
      hx-post="/feedback/@Model.Number/comment">
    @Html.AntiForgeryToken()
```

- [ ] **Step 3: Edit FeedbackList/Default.cshtml**

Add `<span class="sb-issue-number">#@issue.Number</span>` as the first child of each `<li class="sb-issue-item ...">`, before the `<a href=...>` title link.
After edit each list item reads:
```cshtml
<li class="sb-issue-item sb-issue-@issue.State.ToLower()">
    <span class="sb-issue-number">#@issue.Number</span>
    <a href="/feedback/@issue.Number" class="sb-issue-title">@issue.Title</a>
    <span class="sb-issue-meta">
        <span class="sb-issue-state">@issue.State</span>
        <span class="sb-issue-date">@issue.UpdatedAt.ToString("yyyy-MM-dd")</span>
    </span>
</li>
```

- [ ] **Step 4: Verify build passes**

```bash
dotnet build /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run tests**

```bash
dotnet test /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln --verbosity quiet
```
Expected: 24 passed, 0 failed.

- [ ] **Step 6: Verify no per-form hx-headers remains**

```bash
grep -rn "hx-headers" /Users/scottgalloway/RiderProjects/styloissues/src/StyloIssues.UI/
```
Expected: zero matches.

- [ ] **Step 7: Commit**

```bash
cd /Users/scottgalloway/RiderProjects/styloissues && git add src/StyloIssues.UI/Views/Shared/Components/FeedbackForm/Default.cshtml src/StyloIssues.UI/Views/Shared/Components/FeedbackDetail/Default.cshtml src/StyloIssues.UI/Views/Shared/Components/FeedbackList/Default.cshtml && git commit -m "refactor(ui): remove per-form hx-headers; global configRequest is sole htmx token source; add issue number spans"
```

---

### Task 2: Part B - Fix README security notes section

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: nothing
- Produces: README with accurate antiforgery section replacing the stale DisableAntiforgery language

- [ ] **Step 1: Replace the Security notes section in README.md**

Find the `## Security notes` section (lines 41-49) and replace it with:

```markdown
## Security notes

**CSRF:** Antiforgery is enforced by the package. `AddStyloIssuesUi` registers the antiforgery service with `HeaderName = "RequestVerificationToken"`. The form partials render the token as a hidden field via `@Html.AntiForgeryToken()`. The write handlers (`/feedback/new` and `/feedback/{n}/comment`) call `antiforgery.ValidateRequestAsync(context)` after the auth and bot-gate checks. `DisableAntiforgery()` is applied only to the HMAC-signed webhook endpoint (`/feedback/webhook`), which receives server-to-server delivery from GitHub.

Hosts using HTMX must attach the token to HTMX requests with the standard `htmx:configRequest` listener:

```javascript
document.body.addEventListener('htmx:configRequest', e => {
  const t = document.querySelector('input[name="__RequestVerificationToken"]');
  if (t) e.detail.headers['RequestVerificationToken'] = t.value;
});
```

The sample layout (`samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml`) includes this script. No-JS form submissions use the hidden field directly.
```

- [ ] **Step 2: Verify build and tests still pass**

```bash
dotnet build /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln && dotnet test /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln --verbosity quiet
```
Expected: Build succeeded, 24 passed, 0 failed.

- [ ] **Step 3: Commit**

```bash
cd /Users/scottgalloway/RiderProjects/styloissues && git add README.md && git commit -m "docs(readme): replace stale antiforgery section with accurate enforcement description"
```

---

### Task 3: Part C - Sample csproj + wwwroot/css + layout infrastructure

**Files:**
- Modify: `samples/StyloIssues.Sample/StyloIssues.Sample.csproj`
- Create: `samples/StyloIssues.Sample/wwwroot/css/site.css`
- Create: `samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml`
- Create: `samples/StyloIssues.Sample/Pages/_ViewStart.cshtml`

**Interfaces:**
- Consumes: nothing from earlier tasks in the sample
- Produces: the Htmx NuGet package available; the CSS design system; the layout with sticky header, fonts, asset loads, antiforgery script; _ViewStart pointing to the layout

- [ ] **Step 1: Add Htmx NuGet package to sample csproj**

Edit `samples/StyloIssues.Sample/StyloIssues.Sample.csproj` to add:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <UserSecretsId>styloissues-sample-demo</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/StyloIssues.UI/StyloIssues.UI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Htmx" />
  </ItemGroup>

</Project>
```

Then run:
```bash
dotnet restore /Users/scottgalloway/RiderProjects/styloissues/samples/StyloIssues.Sample/StyloIssues.Sample.csproj
```

- [ ] **Step 2: Create wwwroot/css/site.css**

Create directory and file:
```bash
mkdir -p /Users/scottgalloway/RiderProjects/styloissues/samples/StyloIssues.Sample/wwwroot/css
```

Then create `/Users/scottgalloway/RiderProjects/styloissues/samples/StyloIssues.Sample/wwwroot/css/site.css` with the full editorial design system (see Task 3 CSS content below).

**Full CSS content:**

```css
/* === Design tokens === */
:root {
  --paper: #f6f2e9;
  --panel: #efe9dc;
  --ink: #211d18;
  --ink-soft: #6b6156;
  --line: #ddd4c4;
  --accent: #c2412d;
  --accent-ink: #fbf6ee;
  --open: #2f7d4f;
  --open-bg: #e3efe4;
  --closed: #7c5cbf;
  --closed-bg: #ebe4f6;
}

/* === Reset + base === */
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

html { font-size: 16px; scroll-behavior: smooth; }

body {
  font-family: "Hanken Grotesk", system-ui, sans-serif;
  font-size: 1rem;
  line-height: 1.7;
  color: var(--ink);
  background-color: var(--paper);
  min-height: 100vh;
}

/* Engineer grid overlay */
body::before {
  content: "";
  position: fixed;
  inset: 0;
  z-index: -2;
  pointer-events: none;
  opacity: 0.06;
  background-image:
    repeating-linear-gradient(0deg, var(--line) 0, var(--line) 1px, transparent 1px, transparent 28px),
    repeating-linear-gradient(90deg, var(--line) 0, var(--line) 1px, transparent 1px, transparent 28px);
}

/* SVG noise grain overlay */
body::after {
  content: "";
  position: fixed;
  inset: 0;
  z-index: -1;
  pointer-events: none;
  opacity: 0.03;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='200'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='200' height='200' filter='url(%23n)'/%3E%3C/svg%3E");
}

/* View transitions */
@view-transition { navigation: auto; }

/* === Typography === */
h1, h2, h3, h4 {
  font-family: "Fraunces", Georgia, serif;
  font-optical-sizing: auto;
  line-height: 1.2;
}

h1 { font-size: 2rem; font-weight: 900; }
h2 { font-size: 1.5rem; font-weight: 600; }
h3 { font-size: 1.15rem; font-weight: 600; }

a { color: var(--accent); text-decoration: none; }
a:hover { text-decoration: underline; }

/* === HTMX progress bar === */
#htmx-indicator {
  position: fixed;
  top: 0;
  left: 0;
  width: 0;
  height: 3px;
  background: var(--accent);
  z-index: 9999;
  opacity: 0;
  transition: opacity 0.2s, width 0.4s ease;
}
.htmx-request #htmx-indicator {
  opacity: 1;
  width: 70%;
}
.htmx-request.htmx-settling #htmx-indicator {
  width: 100%;
  opacity: 0;
}

/* === Sticky header === */
.site-header {
  position: sticky;
  top: 0;
  z-index: 100;
  background: var(--paper);
  border-bottom: 1px solid var(--line);
  box-shadow: 0 2px 0 0 var(--accent);
  padding: 0.75rem 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.site-header-brand {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.site-wordmark {
  font-family: "Fraunces", Georgia, serif;
  font-weight: 900;
  font-size: 1.35rem;
  color: var(--ink);
  letter-spacing: -0.02em;
  line-height: 1;
}

.site-sublabel {
  font-family: "Space Mono", monospace;
  font-size: 0.65rem;
  color: var(--ink-soft);
  letter-spacing: 0.02em;
  margin-top: 0.15rem;
}

.site-user-chip {
  font-family: "Space Mono", monospace;
  font-size: 0.7rem;
  color: var(--ink-soft);
  border: 1px solid var(--line);
  border-radius: 3px;
  padding: 0.25rem 0.6rem;
  white-space: nowrap;
  background: var(--panel);
}

/* === Main content column === */
.site-main {
  max-width: 900px;
  margin: 0 auto;
  padding: 2.5rem 1.5rem 4rem;
}

/* === Page sections === */
.section-heading {
  font-family: "Fraunces", Georgia, serif;
  font-weight: 600;
  font-size: 1.1rem;
  color: var(--ink);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  margin-bottom: 1.25rem;
  padding-bottom: 0.4rem;
  border-bottom: 1px solid var(--line);
}

.content-section { margin-bottom: 3rem; }

/* === Form panel === */
.sb-feedback-form,
.form-panel {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 4px;
  padding: 1.75rem;
}

/* === Fields === */
.sb-field {
  margin-bottom: 1.25rem;
}

.sb-field label {
  display: block;
  font-family: "Space Mono", monospace;
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--ink-soft);
  margin-bottom: 0.4rem;
}

.sb-field input[type="text"],
.sb-field input[type="email"],
.sb-field textarea,
.sb-field select {
  display: block;
  width: 100%;
  font-family: "Hanken Grotesk", system-ui, sans-serif;
  font-size: 0.95rem;
  color: var(--ink);
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 3px;
  padding: 0.6rem 0.75rem;
  transition: border-color 0.15s, box-shadow 0.15s;
  outline: none;
  appearance: none;
}

.sb-field input:focus,
.sb-field textarea:focus,
.sb-field select:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 15%, transparent);
}

.sb-field textarea { resize: vertical; min-height: 8rem; }

/* === Actions + buttons === */
.sb-actions { display: flex; align-items: center; gap: 0.75rem; margin-top: 0.5rem; }

.sb-actions button {
  font-family: "Space Mono", monospace;
  font-size: 0.8rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  color: var(--accent-ink);
  background: var(--accent);
  border: none;
  border-radius: 3px;
  padding: 0.55rem 1.2rem;
  cursor: pointer;
  transition: background 0.15s, transform 0.1s;
}

.sb-actions button:hover { background: color-mix(in srgb, var(--accent) 85%, var(--ink)); transform: translateY(-1px); }
.sb-actions button:active { transform: translateY(0); }
.sb-actions button:disabled { opacity: 0.5; cursor: not-allowed; transform: none; }

/* === Issue list === */
.sb-issue-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.sb-issue-item {
  background: var(--panel);
  border: 1px solid var(--line);
  border-left: 3px solid transparent;
  border-radius: 4px;
  padding: 0.85rem 1rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  transition: border-left-color 0.15s, transform 0.15s, box-shadow 0.15s;
}

.sb-issue-item:hover {
  border-left-color: var(--accent);
  transform: translateY(-1px);
  box-shadow: 0 2px 6px color-mix(in srgb, var(--ink) 8%, transparent);
}

.sb-issue-number {
  font-family: "Space Mono", monospace;
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--accent);
  flex-shrink: 0;
}

.sb-issue-title {
  font-family: "Hanken Grotesk", system-ui, sans-serif;
  font-weight: 500;
  color: var(--ink);
  flex: 1;
}

.sb-issue-title:hover { text-decoration: underline; }

.sb-issue-meta {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-shrink: 0;
  margin-left: auto;
}

.sb-issue-state {
  font-family: "Space Mono", monospace;
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  padding: 0.15rem 0.5rem;
  border-radius: 3px;
}

.sb-issue-open .sb-issue-state,
.sb-issue-state.sb-issue-open,
.sb-issue-item.sb-issue-open .sb-issue-state {
  color: var(--open);
  background: var(--open-bg);
}

.sb-issue-closed .sb-issue-state,
.sb-issue-state.sb-issue-closed,
.sb-issue-item.sb-issue-closed .sb-issue-state {
  color: var(--closed);
  background: var(--closed-bg);
}

.sb-issue-date {
  font-family: "Space Mono", monospace;
  font-size: 0.65rem;
  color: var(--ink-soft);
}

/* === Issue detail === */
.sb-issue-detail { max-width: 720px; }

.sb-issue-header {
  margin-bottom: 1.75rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--line);
}

.sb-issue-header .sb-issue-number {
  font-family: "Space Mono", monospace;
  font-size: 1rem;
  color: var(--accent);
  display: block;
  margin-bottom: 0.35rem;
}

.sb-issue-header h2 {
  font-family: "Fraunces", Georgia, serif;
  font-weight: 600;
  font-size: 1.65rem;
  color: var(--ink);
  margin-bottom: 0.5rem;
}

.sb-issue-header .sb-issue-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-left: 0;
  justify-content: flex-start;
}

.sb-issue-body {
  font-family: "Hanken Grotesk", system-ui, sans-serif;
  font-size: 1rem;
  line-height: 1.8;
  color: var(--ink);
  margin-bottom: 2rem;
}

.sb-github-link {
  font-family: "Space Mono", monospace;
  font-size: 0.7rem;
  color: var(--ink-soft);
}

/* === Comments === */
.sb-comments {
  margin-bottom: 2rem;
}

.sb-comments h3 { margin-bottom: 1rem; }

.sb-comment {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 4px;
  padding: 0.85rem 1rem;
  margin-bottom: 0.75rem;
}

.sb-comment.sb-comment-bot { border-left: 3px solid var(--accent); }

.sb-comment-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.sb-comment-author {
  font-family: "Space Mono", monospace;
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--ink);
}

.sb-comment-badge {
  font-family: "Space Mono", monospace;
  font-size: 0.65rem;
  color: var(--accent);
  background: color-mix(in srgb, var(--accent) 12%, transparent);
  padding: 0.1rem 0.35rem;
  border-radius: 3px;
}

.sb-comment-date {
  font-family: "Space Mono", monospace;
  font-size: 0.65rem;
  color: var(--ink-soft);
  margin-left: auto;
}

.sb-comment-body {
  font-size: 0.95rem;
  line-height: 1.7;
  color: var(--ink);
}

/* === Add comment form === */
.sb-add-comment {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 4px;
  padding: 1.25rem 1.25rem 1rem;
}

.sb-add-comment h3 { margin-bottom: 1rem; font-size: 1rem; }

/* === Empty / not-found states === */
.sb-empty,
.sb-not-found {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--ink-soft);
  font-family: "Hanken Grotesk", system-ui, sans-serif;
}

.sb-empty p, .sb-not-found p {
  font-size: 1rem;
  margin-bottom: 0.5rem;
}

/* === Bot / challenge verdicts === */
.sb-verdict {
  background: color-mix(in srgb, var(--accent) 8%, var(--paper));
  border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--line));
  border-radius: 4px;
  padding: 1rem 1.25rem;
}

.sb-verdict-detail { font-size: 0.85rem; color: var(--ink-soft); }

.sb-challenge {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 4px;
  padding: 1rem 1.25rem;
}

/* === Back link === */
.back-link {
  display: inline-block;
  font-family: "Space Mono", monospace;
  font-size: 0.75rem;
  color: var(--ink-soft);
  margin-bottom: 1.5rem;
}

.back-link:hover { color: var(--accent); text-decoration: none; }

/* === Page-load staggered reveal === */
@keyframes fade-up {
  from { opacity: 0; transform: translateY(12px); }
  to   { opacity: 1; transform: translateY(0); }
}

.animate-in {
  animation: fade-up 0.35s ease both;
}

.site-header    { animation: fade-up 0.3s ease both; animation-delay: 0ms; }
.form-panel     { animation: fade-up 0.35s ease both; animation-delay: 80ms; }
.sb-feedback-form { animation: fade-up 0.35s ease both; animation-delay: 80ms; }
.sb-issue-list  { animation: fade-up 0.35s ease both; animation-delay: 150ms; }
.sb-issue-item:nth-child(1)  { animation: fade-up 0.3s ease both; animation-delay: 160ms; }
.sb-issue-item:nth-child(2)  { animation: fade-up 0.3s ease both; animation-delay: 190ms; }
.sb-issue-item:nth-child(3)  { animation: fade-up 0.3s ease both; animation-delay: 220ms; }
.sb-issue-item:nth-child(4)  { animation: fade-up 0.3s ease both; animation-delay: 250ms; }
.sb-issue-item:nth-child(n+5){ animation: fade-up 0.3s ease both; animation-delay: 280ms; }

/* Respect reduced-motion preference */
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation: none !important; transition: none !important; }
}
```

- [ ] **Step 3: Create Pages/Shared/_Layout.cshtml**

Create directory: `mkdir -p samples/StyloIssues.Sample/Pages/Shared`

Create the file `/Users/scottgalloway/RiderProjects/styloissues/samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml`:

```cshtml
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>@(ViewData["Title"] ?? "StyloIssues")</title>
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous" />
  <link href="https://fonts.googleapis.com/css2?family=Fraunces:ital,opsz,wght@0,9..144,400;0,9..144,600;0,9..144,900;1,9..144,400&family=Hanken+Grotesk:wght@400;500;700&family=Space+Mono:wght@400;700&display=swap" rel="stylesheet" />
  <link rel="stylesheet" href="~/css/site.css" />
</head>
<body hx-boost="true">
  <div id="htmx-indicator"></div>

  <header class="site-header">
    <div class="site-header-brand">
      <a href="/feedback" class="site-wordmark">StyloIssues</a>
      <span class="site-sublabel">feedback / scottgal/styloissues-demo</span>
    </div>
    <span class="site-user-chip">filing as Demo User</span>
  </header>

  <main class="site-main">
    @RenderBody()
  </main>

  @Html.AntiForgeryToken()

  <script src="~/_content/StyloIssues.UI/htmx.min.js"></script>
  <script defer src="~/_content/StyloIssues.UI/alpine.min.js"></script>

  <script>
    // Attach antiforgery token to every HTMX request via the standard configRequest event.
    // The token value is read from the hidden field rendered by @Html.AntiForgeryToken() above.
    document.body.addEventListener('htmx:configRequest', function(e) {
      var t = document.querySelector('input[name="__RequestVerificationToken"]');
      if (t) e.detail.headers['RequestVerificationToken'] = t.value;
    });

    // Re-initialize Alpine for content swapped in by HTMX (hx-boost, hx-swap, etc.).
    document.body.addEventListener('htmx:afterSettle', function(e) {
      if (window.Alpine && e.detail.elt) window.Alpine.initTree(e.detail.elt);
    });
  </script>
</body>
</html>
```

- [ ] **Step 4: Create Pages/_ViewStart.cshtml**

Create `/Users/scottgalloway/RiderProjects/styloissues/samples/StyloIssues.Sample/Pages/_ViewStart.cshtml`:

```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 5: Build and restore**

```bash
dotnet build /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln
```
Expected: Build succeeded, 0 errors. The Htmx package will be resolved from NuGet.

- [ ] **Step 6: Run all tests**

```bash
dotnet test /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln --verbosity quiet
```
Expected: 24 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
cd /Users/scottgalloway/RiderProjects/styloissues && git add samples/StyloIssues.Sample/StyloIssues.Sample.csproj samples/StyloIssues.Sample/wwwroot/css/site.css samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml samples/StyloIssues.Sample/Pages/_ViewStart.cshtml && git commit -m "feat(sample): add Htmx.Net, editorial CSS design system, shared layout with global antiforgery script"
```

---

### Task 4: Part C - Update sample pages to use layout

**Files:**
- Modify: `samples/StyloIssues.Sample/Pages/Feedback.cshtml`
- Modify: `samples/StyloIssues.Sample/Pages/FeedbackDetail.cshtml`

**Interfaces:**
- Consumes: `_Layout.cshtml` from Task 3 (the `_ViewStart.cshtml` sets Layout = "_Layout" automatically)
- Produces: both pages use the layout; Feedback.cshtml wraps form + list in named sections; FeedbackDetail renders with back link

- [ ] **Step 1: Replace Feedback.cshtml**

Replace the entire file content with:

```cshtml
@page
@{
    ViewData["Title"] = "File a report";
}

<div class="content-section">
    <h2 class="section-heading">File a report</h2>
    <sb-feedback-form></sb-feedback-form>
</div>

<div class="content-section">
    <h2 class="section-heading">Your reports</h2>
    <sb-feedback-list />
</div>
```

- [ ] **Step 2: Replace FeedbackDetail.cshtml**

Replace the entire file content with:

```cshtml
@page "/feedback/{number:int}"
@{
    var number = Convert.ToInt32(RouteData.Values["number"]);
    ViewData["Title"] = "Feedback #" + number;
}

<a href="/feedback" class="back-link">back to all reports</a>

<sb-feedback-detail number="@number" />
```

- [ ] **Step 3: Run all tests**

```bash
dotnet test /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln --verbosity quiet
```
Expected: 24 passed, 0 failed.
The existing tests assert on `sb-issue-list`, `sb-issue-detail`, `Demo Bug Report`, etc. - all class markers remain in the partials, so the assertions still pass even with the new layout wrapper.

- [ ] **Step 4: Check no em dashes in changed files**

```bash
grep -r "\xe2\x80\x94\|\x97\|&#x2014;" /Users/scottgalloway/RiderProjects/styloissues/samples/ /Users/scottgalloway/RiderProjects/styloissues/src/StyloIssues.UI/Views/ /Users/scottgalloway/RiderProjects/styloissues/README.md
```
Expected: zero matches.

- [ ] **Step 5: Confirm no per-form hx-headers in package partials**

```bash
grep -rn "hx-headers" /Users/scottgalloway/RiderProjects/styloissues/src/
```
Expected: zero matches.

- [ ] **Step 6: Confirm @Html.AntiForgeryToken() still present in partials**

```bash
grep -rn "AntiForgeryToken" /Users/scottgalloway/RiderProjects/styloissues/src/StyloIssues.UI/Views/
```
Expected: at least 2 matches (one in FeedbackForm/Default.cshtml, one in FeedbackDetail/Default.cshtml).

- [ ] **Step 7: Commit**

```bash
cd /Users/scottgalloway/RiderProjects/styloissues && git add samples/StyloIssues.Sample/Pages/Feedback.cshtml samples/StyloIssues.Sample/Pages/FeedbackDetail.cshtml && git commit -m "feat(sample): update pages to use editorial layout; remove bare html wrappers"
```

---

### Task 5: Append to final-review-fixes.md + final verification + squash commit

**Files:**
- Modify: `.superpowers/sdd/final-review-fixes.md`

**Interfaces:**
- Consumes: all prior task outputs
- Produces: final-review-fixes.md updated; single combined commit; all checks pass

- [ ] **Step 1: Append section to final-review-fixes.md**

Append this content to `/Users/scottgalloway/RiderProjects/styloissues/.superpowers/sdd/final-review-fixes.md`:

```markdown
---

## Styled demo + Htmx.Net + antiforgery cleanup

Applied 2026-07-07. Branch: main.

### Antiforgery model

Single token source per request: the `_Layout.cshtml` emits one `@Html.AntiForgeryToken()` hidden field. The global `htmx:configRequest` listener in the layout reads this field and attaches the value as `RequestVerificationToken` header on every HTMX request. The form partials retain their own `@Html.AntiForgeryToken()` for no-JS form submission fallback. The per-form `hx-headers` attributes (belt-and-braces, now removed) previously duplicated the token injection.

Server enforcement is unchanged: both `HandleNewIssueAsync` and `HandleAddCommentAsync` call `antiforgery.ValidateRequestAsync(context)` after the auth and Bare-gate checks. `DisableAntiforgery()` applies only to the HMAC-signed webhook.

### Files changed

**Package partials (src/StyloIssues.UI/Views/Shared/Components/):**
- `FeedbackForm/Default.cshtml`: removed `@inject IAntiforgery Af`, `@inject IHttpContextAccessor Hca`, `var tok = ...`, and `hx-headers` attribute from form. Kept `@Html.AntiForgeryToken()`.
- `FeedbackDetail/Default.cshtml`: same removals from comment form. Added `<span class="sb-issue-number">#@Model.Number</span>` before `<h2>` in header. Kept `@Html.AntiForgeryToken()`.
- `FeedbackList/Default.cshtml`: added `<span class="sb-issue-number">#@issue.Number</span>` as first child of each `<li>`.

**README.md:** replaced stale "Security notes" section (which said DisableAntiforgery was on write endpoints) with accurate description of enforcement.

**Sample (samples/StyloIssues.Sample/):**
- `StyloIssues.Sample.csproj`: added `<PackageReference Include="Htmx" />` (Htmx.Net).
- `wwwroot/css/site.css` (created): full "drafting-table editorial" design system with CSS custom properties, Fraunces/Hanken Grotesk/Space Mono fonts, engineer grid overlay, SVG noise grain, sticky header, editorial issue list/detail/form styles, staggered page-load animations, reduced-motion support, htmx progress indicator, view-transitions.
- `Pages/Shared/_Layout.cshtml` (created): sticky header, Google Fonts link, css/site.css, htmx+alpine from `_content/StyloIssues.UI/`, `hx-boost="true"` body, `@Html.AntiForgeryToken()`, global configRequest + Alpine initTree scripts.
- `Pages/_ViewStart.cshtml` (created): `Layout = "_Layout"`.
- `Pages/Feedback.cshtml`: removed bare html wrapper; uses layout; editorial section headings.
- `Pages/FeedbackDetail.cshtml`: removed bare html wrapper; uses layout; back link.
```

- [ ] **Step 2: Final build + full test run**

```bash
dotnet build /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln && dotnet test /Users/scottgalloway/RiderProjects/styloissues/StyloIssues.sln
```
Expected: Build succeeded, 24 passed, 0 failed.

- [ ] **Step 3: Em-dash audit across all changed files**

```bash
grep -rn -- $'\xe2\x80\x94' /Users/scottgalloway/RiderProjects/styloissues/samples/ /Users/scottgalloway/RiderProjects/styloissues/src/ /Users/scottgalloway/RiderProjects/styloissues/README.md /Users/scottgalloway/RiderProjects/styloissues/.superpowers/
```
Expected: zero matches.

- [ ] **Step 4: Final commit of docs update and combined feat commit**

```bash
cd /Users/scottgalloway/RiderProjects/styloissues && git add .superpowers/sdd/final-review-fixes.md && git commit -m "feat(demo): styled HTMX+Alpine SPA (Htmx.Net), global antiforgery, README fix"
```
