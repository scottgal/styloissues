# StyloIssues

[![CI](https://github.com/scottgal/styloissues/actions/workflows/ci.yml/badge.svg)](https://github.com/scottgal/styloissues/actions/workflows/ci.yml)
![version](https://img.shields.io/badge/version-0.0.0-blue)
[![NuGet](https://img.shields.io/nuget/v/StyloIssues.svg)](https://www.nuget.org/packages/StyloIssues)
![license](https://img.shields.io/badge/license-Unlicense-blue)

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

**CSRF:** Antiforgery is enforced by the package. `AddStyloIssuesUi` registers the antiforgery
service with `HeaderName = "RequestVerificationToken"`. The form partials render the token as a
hidden field via `@Html.AntiForgeryToken()`. The write handlers (`/feedback/new` and
`/feedback/{n}/comment`) call `antiforgery.ValidateRequestAsync(context)` after the auth and
bot-gate checks. `DisableAntiforgery()` is applied only to the HMAC-signed webhook endpoint
(`/feedback/webhook`), which receives server-to-server delivery from GitHub.

Hosts using HTMX must attach the token to HTMX requests with the standard `htmx:configRequest`
listener:

```javascript
document.body.addEventListener('htmx:configRequest', e => {
  const t = document.querySelector('input[name="__RequestVerificationToken"]');
  if (t) e.detail.headers['RequestVerificationToken'] = t.value;
});
```

The sample layout (`samples/StyloIssues.Sample/Pages/Shared/_Layout.cshtml`) includes this script.
No-JS form submissions use the hidden field directly.

## Status

Early implementation. See `docs/` for the design spec and the task-by-task plan.

## License

Released into the public domain under [The Unlicense](LICENSE).