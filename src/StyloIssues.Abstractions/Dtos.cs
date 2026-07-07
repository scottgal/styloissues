namespace StyloIssues.Abstractions;

public sealed record NewIssueRequest(string Title, string Body, string Category);

public record IssueSummary(int Number, string Title, string State, DateTimeOffset UpdatedAt, string HtmlUrl);

public sealed record IssueComment(string Author, string Body, DateTimeOffset CreatedAt, bool FromApp);

public sealed record IssueDetail(
    int Number, string Title, string State, DateTimeOffset UpdatedAt, string HtmlUrl,
    string Body, IReadOnlyList<IssueComment> Comments)
    : IssueSummary(Number, Title, State, UpdatedAt, HtmlUrl);

/// <summary>Attribution context passed to the gateway on every write.</summary>
public sealed record ReporterContext(string DisplayName, string? GitHubLogin, string Marker);

public enum FeedbackFormState { Full, ChallengeGated, Bare }

public sealed record FeedbackVerdictView(
    FeedbackFormState State, string? BotName, string? BotType,
    double Probability, string ThreatBand, string? Reason);

/// <summary>
/// Diagnostic archive produced by an optional host-supplied <see cref="IIssueAttachmentSource"/>.
/// Bytes may be null when the archive is hosted externally and only a Url is available.
/// </summary>
public sealed record IssueAttachment(
    string FileName, byte[]? Bytes, string? Url, string ContentType, string? ManifestSummary);
