using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.UI.ViewComponents;

public sealed class FeedbackListViewComponent : ViewComponent
{
    private readonly IIssueReader _reader;
    private readonly ICurrentUser _user;
    private readonly StyloIssuesOptions _opts;

    public FeedbackListViewComponent(
        IIssueReader reader,
        ICurrentUser user,
        IOptions<StyloIssuesOptions> opts)
    {
        _reader = reader;
        _user = user;
        _opts = opts.Value;
    }

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken ct = default)
    {
        IReadOnlyList<IssueSummary> issues;

        if (_user.IsAuthenticated && _user.StableId is not null)
        {
            var marker = ReporterMarker.Compute(
                Encoding.UTF8.GetBytes(_opts.MarkerKey), _user.StableId);
            issues = await _reader.ListByReporterAsync(marker, ct);
        }
        else if (_opts.EnablePublicList)
        {
            issues = await _reader.ListPublicAsync(ct);
        }
        else
        {
            issues = Array.Empty<IssueSummary>();
        }

        return View(issues);
    }
}
