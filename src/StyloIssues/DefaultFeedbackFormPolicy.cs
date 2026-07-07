using Microsoft.AspNetCore.Http;
using StyloIssues.Abstractions;

namespace StyloIssues;

public sealed class DefaultFeedbackFormPolicy : IFeedbackFormPolicy
{
    public FeedbackVerdictView Evaluate(HttpContext context, ICurrentUser user) =>
        new(FeedbackFormState.Full, null, null, 0, "none", null);
}
