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

## Status

Early implementation. See `docs/` for the design spec and the task-by-task plan.

## License

TBD.