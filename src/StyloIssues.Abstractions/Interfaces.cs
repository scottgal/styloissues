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
