using StyloIssues.Abstractions;

namespace StyloIssues;

public sealed class NullIssueStore : IIssueStore
{
    public Task UpsertAsync(IssueDetail issue, string? reporterMarker, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IssueSummary>>([]);
}
