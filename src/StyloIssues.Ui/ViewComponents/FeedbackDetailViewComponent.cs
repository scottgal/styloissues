using Microsoft.AspNetCore.Mvc;
using StyloIssues.Abstractions;

namespace StyloIssues.UI.ViewComponents;

public sealed class FeedbackDetailViewComponent : ViewComponent
{
    private readonly IIssueReader _reader;

    public FeedbackDetailViewComponent(IIssueReader reader)
    {
        _reader = reader;
    }

    public async Task<IViewComponentResult> InvokeAsync(int number, CancellationToken ct = default)
    {
        var issue = await _reader.GetIssueAsync(number, ct);
        return View(issue);
    }
}
