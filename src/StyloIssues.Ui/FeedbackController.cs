using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;
using StyloIssues.Webhook;

namespace StyloIssues.Ui;

[Route("feedback")]
public sealed class FeedbackController : Controller
{
    private readonly IIssueReader _reader;
    private readonly IIssueGateway _gateway;
    private readonly ICurrentUser _user;
    private readonly IFeedbackFormPolicy _policy;
    private readonly StyloIssuesOptions _opts;
    private readonly WebhookHandler _webhookHandler;

    public FeedbackController(
        IIssueReader reader,
        IIssueGateway gateway,
        ICurrentUser user,
        IFeedbackFormPolicy policy,
        IOptions<StyloIssuesOptions> opts,
        WebhookHandler webhookHandler)
    {
        _reader = reader;
        _gateway = gateway;
        _user = user;
        _policy = policy;
        _opts = opts.Value;
        _webhookHandler = webhookHandler;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        IReadOnlyList<IssueSummary> issues;

        if (_user.IsAuthenticated && _user.StableId is not null)
        {
            var marker = ReporterMarker.Compute(Encoding.UTF8.GetBytes(_opts.MarkerKey), _user.StableId);
            issues = await _reader.ListByReporterAsync(marker, ct);
        }
        else if (_opts.EnablePublicList)
        {
            issues = await _reader.ListPublicAsync(ct);
        }
        else
        {
            issues = [];
        }

        return View("_List", issues);
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        var verdict = _policy.Evaluate(HttpContext, _user);
        return View("New", verdict);
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(
        [FromForm] string title,
        [FromForm] string body,
        [FromForm] string category,
        CancellationToken ct)
    {
        if (!_user.IsAuthenticated) return Challenge();

        // Server-side gate: re-evaluate policy on every POST; never trust the hidden button.
        var verdict = _policy.Evaluate(HttpContext, _user);
        if (verdict.State == FeedbackFormState.Bare)
            return StatusCode(StatusCodes.Status403Forbidden);

        var marker = ReporterMarker.Compute(Encoding.UTF8.GetBytes(_opts.MarkerKey), _user.StableId!);
        var issue = await _gateway.CreateIssueAsync(
            new NewIssueRequest(title, body, category),
            new ReporterContext(_user.DisplayName, _user.GitHubLogin, marker),
            ct);

        return RedirectToAction(nameof(Detail), new { number = issue.Number });
    }

    [HttpGet("{number:int}")]
    public async Task<IActionResult> Detail(int number, CancellationToken ct)
    {
        var issue = await _reader.GetIssueAsync(number, ct);
        if (issue is null) return NotFound();
        return View("Detail", issue);
    }

    [HttpPost("{number:int}/comment")]
    public async Task<IActionResult> Comment(int number, [FromForm] string body, CancellationToken ct)
    {
        if (!_user.IsAuthenticated) return Challenge();

        var marker = ReporterMarker.Compute(Encoding.UTF8.GetBytes(_opts.MarkerKey), _user.StableId!);
        await _gateway.AddCommentAsync(
            number, body,
            new ReporterContext(_user.DisplayName, _user.GitHubLogin, marker),
            ct);

        return RedirectToAction(nameof(Detail), new { number });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var payload = ms.ToArray();

        var sig = Request.Headers["X-Hub-Signature-256"].FirstOrDefault() ?? "";
        if (!WebhookVerifier.IsValid(payload, sig, _opts.WebhookSecret))
            return Unauthorized();

        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault() ?? "";
        using var doc = JsonDocument.Parse(payload.Length > 0 ? payload : "{}"u8.ToArray());
        await _webhookHandler.HandleAsync(eventType, doc.RootElement);

        return Ok();
    }
}
