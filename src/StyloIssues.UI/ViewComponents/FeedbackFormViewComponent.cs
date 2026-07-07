using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StyloIssues.Abstractions;

namespace StyloIssues.UI.ViewComponents;

public sealed class FeedbackFormViewComponent : ViewComponent
{
    private readonly IFeedbackFormPolicy _policy;
    private readonly ICurrentUser _user;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FeedbackFormViewComponent(
        IFeedbackFormPolicy policy,
        ICurrentUser user,
        IHttpContextAccessor httpContextAccessor)
    {
        _policy = policy;
        _user = user;
        _httpContextAccessor = httpContextAccessor;
    }

    public IViewComponentResult Invoke()
    {
        var ctx = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available.");
        var verdict = _policy.Evaluate(ctx, _user);
        return View(verdict);
    }
}
