using Microsoft.AspNetCore.Http;
using StyloIssues;
using StyloIssues.Abstractions;
using Moq;
using Xunit;

public class DefaultFeedbackFormPolicyTests
{
    [Fact]
    public void Default_policy_is_full_form()
    {
        var user = new Mock<ICurrentUser>().Object;
        var view = new DefaultFeedbackFormPolicy().Evaluate(new DefaultHttpContext(), user);
        Assert.Equal(FeedbackFormState.Full, view.State);
    }
}
