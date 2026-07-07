# StyloIssues

A GitHub-issues-backed feedback UX for ASP.NET Core. Users file bug reports and
feature requests through a first-class UI on your site; those flow two-way to
and from GitHub issues on your repo. GitHub stays the source of truth and gets
the full fix-it workflow (labels, PRs, CI); your site provides a nicer front
door.

Reusable and framework-agnostic: pluggable identity (`ICurrentUser`), pluggable
form policy (`IFeedbackFormPolicy`), an optional read-model (`IIssueStore`), and
an optional diagnostic-archive attachment hook (`IIssueAttachmentSource`).
GitHub is the source of truth, so the default build needs no database.

## Layout

- `src/StyloIssues.Abstractions`: interfaces, DTOs, options, reporter marker.
- `src/StyloIssues`: Octokit GitHub-App gateway, sync, DI wiring.
- `src/StyloIssues.UI`: Razor Class Library: SSR + HTMX + Alpine feedback UI.
- `samples/StyloIssues.Sample`: zero-infra sample host.
- `docs/`: design spec and implementation plan.

## Host route convention

The UI components expect two host-owned pages in your application:

- `GET /feedback` - embeds `<sb-feedback-form>` and `<sb-feedback-list />`
- `GET /feedback/{number:int}` - embeds `<sb-feedback-detail number="@number" />`

See `samples/StyloIssues.Sample/Pages/Feedback.cshtml` and `FeedbackDetail.cshtml` for reference
implementations. These pages are intentionally not provided by the RCL so hosts retain full
control over their layout and surrounding markup.

## IIssueStore

`IIssueStore` is a forward-declared seam for a host-supplied read-model (e.g. a SQLite or
PostgreSQL projection). The default build binds a no-op; no production wiring drives it in this
drop. Implement and register it only if you need a local query surface beyond what the GitHub API
already provides via `IIssueReader`.

## Security notes

**CSRF:** The `/feedback/new` and `/feedback/{n}/comment` write endpoints currently call
`.DisableAntiforgery()` because the forms use HTMX and the package does not assume the host has
antiforgery middleware configured. Hosts that require full antiforgery protection should:
1. Call `services.AddAntiforgery()` in their DI setup.
2. Inject the request-verification token into the form HTML (hidden input for standard POST,
   `hx-headers` for HTMX requests).
3. Remove the `.DisableAntiforgery()` calls from the endpoint registrations.

## Status

Early implementation. See `docs/` for the design spec and the task-by-task plan.

## License

TBD.