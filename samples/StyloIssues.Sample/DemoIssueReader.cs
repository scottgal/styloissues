using StyloIssues.Abstractions;

namespace StyloIssues.Sample;

/// <summary>
/// Canned in-memory IIssueReader for the offline sample. No GitHub credentials required.
/// Replace with the real CachingIssueReader (via AddStyloIssues) when credentials are available.
/// </summary>
public sealed class DemoIssueReader : IIssueReader
{
    private static readonly IReadOnlyList<IssueSummary> DemoIssues =
    [
        new IssueSummary(1, "Demo Bug Report", "open",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/demo/demo/issues/1"),
        new IssueSummary(2, "Feature request: dark mode", "open",
            new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/demo/demo/issues/2"),
        new IssueSummary(3, "Question: how to configure webhook", "closed",
            new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/demo/demo/issues/3"),
    ];

    public Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
    {
        var summary = DemoIssues.FirstOrDefault(i => i.Number == number);
        if (summary is null)
            return Task.FromResult<IssueDetail?>(null);

        var detail = new IssueDetail(
            summary.Number,
            summary.Title,
            summary.State,
            summary.UpdatedAt,
            summary.HtmlUrl,
            "This is a demo issue. Replace with real data from GitHub.",
            []);
        return Task.FromResult<IssueDetail?>(detail);
    }

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct)
        => Task.FromResult(DemoIssues);

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct)
        => Task.FromResult(DemoIssues);

    public void Invalidate(int number) { }
    public void InvalidateAll() { }
}
