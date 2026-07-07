using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;
using StyloIssues.UI.Models;

namespace StyloIssues.UI.ViewComponents;

public sealed class FeedbackFormViewComponent : ViewComponent
{
    private readonly IFeedbackFormPolicy _policy;
    private readonly ICurrentUser _user;
    private readonly StyloIssuesOptions _opts;

    public FeedbackFormViewComponent(
        IFeedbackFormPolicy policy,
        ICurrentUser user,
        IOptions<StyloIssuesOptions> opts)
    {
        _policy = policy;
        _user = user;
        _opts = opts.Value;
    }

    public IViewComponentResult Invoke()
    {
        var verdict = _policy.Evaluate(HttpContext, _user);
        var gitHubIssuesUrl = $"https://github.com/{_opts.RepoOwner}/{_opts.RepoName}/issues";
        return View(new FeedbackFormViewModel(verdict, gitHubIssuesUrl));
    }
}
