using StyloIssues.Abstractions;

namespace StyloIssues.UI.Models;

/// <summary>View model for the FeedbackForm component, combining the policy verdict
/// with host-level context (GitHub issues URL) that the core abstractions don't carry.</summary>
public sealed record FeedbackFormViewModel(
    FeedbackVerdictView Verdict,
    string GitHubIssuesUrl)
{
    // Expose verdict members directly for convenience in the view.
    public FeedbackFormState State => Verdict.State;
    public string? BotName => Verdict.BotName;
    public string? BotType => Verdict.BotType;
    public double Probability => Verdict.Probability;
    public string ThreatBand => Verdict.ThreatBand;
    public string? Reason => Verdict.Reason;
}
