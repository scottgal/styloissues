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
