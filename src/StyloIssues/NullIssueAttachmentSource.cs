using StyloIssues.Abstractions;

namespace StyloIssues;

/// <summary>
/// Default no-op attachment source: returns null so no archive is attached to new issues.
/// Hosts override this binding to supply a real <see cref="IIssueAttachmentSource"/>.
/// </summary>
public sealed class NullIssueAttachmentSource : IIssueAttachmentSource
{
    public Task<IssueAttachment?> CaptureAsync(
        string? fingerprint, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Task.FromResult<IssueAttachment?>(null);
}
