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

/// <summary>
/// Optional host-supplied read-model. Default build binds a no-op (<see cref="StyloIssues.NullIssueStore"/>).
/// <para>
/// This interface is a forward-declared seam: it is defined here so hosts can register a
/// custom implementation (e.g. a SQLite or PostgreSQL projection), but no production wiring
/// in this package currently drives it. The <see cref="StyloIssues.Sync.ReconcilerService"/>
/// calls <see cref="UpsertAsync"/> when it lands, but only if the host replaces the no-op binding.
/// Adding a real backing store is an advanced integration step, not required for the default build.
/// </para>
/// </summary>
public interface IIssueStore
{
    Task UpsertAsync(IssueDetail issue, string? reporterMarker, CancellationToken ct);
    Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct);
}

/// <summary>
/// Optional host hook to attach a diagnostic archive (e.g. StyloDump) to a new issue.
/// StyloIssues passes primitive scope params and stays unaware of how the archive is
/// produced or hosted. Default binding returns null (no attachment).
/// </summary>
public interface IIssueAttachmentSource
{
    Task<IssueAttachment?> CaptureAsync(
        string? fingerprint, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
