using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;
using StyloIssues.Webhook;
using StyloIssues;

namespace StyloIssues.UI;

public static class StyloIssuesEndpoints
{
    public static IEndpointRouteBuilder MapStyloIssues(this IEndpointRouteBuilder app)
    {
        app.MapPost("/feedback/new", HandleNewIssueAsync).DisableAntiforgery();
        app.MapPost("/feedback/{number:int}/comment", HandleAddCommentAsync).DisableAntiforgery();
        app.MapPost("/feedback/webhook", HandleWebhookAsync).DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> HandleNewIssueAsync(
        HttpContext context,
        ICurrentUser user,
        IFeedbackFormPolicy policy,
        IIssueGateway gateway,
        IOptions<StyloIssuesOptions> opts,
        IIssueAttachmentSource attachments,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.StableId is null)
            return Results.Unauthorized();

        // Server-side policy gate: re-evaluate; this is the real enforcement point,
        // not a client-side check.
        var verdict = policy.Evaluate(context, user);
        // Only FeedbackFormState.Bare (confirmed bot) is hard-blocked here with a 403.
        // FeedbackFormState.ChallengeGated (suspicious, may be human) is intentionally NOT
        // blocked by the core package: challenge handling is a host/bridge responsibility
        // (e.g. redirecting to a CAPTCHA or showing challenge UI before allowing submission).
        if (verdict.State == FeedbackFormState.Bare)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var form = await context.Request.ReadFormAsync(ct);
        var title = form["title"].ToString().Trim();
        var body = form["body"].ToString().Trim();
        var category = form["category"].ToString().Trim();
        var attach = form["attach"].ToString();

        if (string.IsNullOrEmpty(title))
            return Results.BadRequest("title is required");

        var reqBody = body;
        if (attach == "on")
        {
            var now = timeProvider.GetUtcNow();
            var fingerprint = context.Request.Headers["X-SB-Fingerprint"].FirstOrDefault();
            var att = await attachments.CaptureAsync(fingerprint, now.AddHours(-1), now, ct);
            if (att is not null)
                reqBody = AttachmentBody.Append(reqBody, att);
        }

        var marker = ReporterMarker.Compute(Encoding.UTF8.GetBytes(opts.Value.MarkerKey), user.StableId!);

        var reporter = new ReporterContext(user.DisplayName, user.GitHubLogin, marker);
        var issue = await gateway.CreateIssueAsync(new NewIssueRequest(title, reqBody, category), reporter, ct);

        // HTMX clients receive HX-Redirect; standard clients get 302.
        if (context.Request.Headers.ContainsKey("HX-Request"))
        {
            context.Response.Headers.Append("HX-Redirect", $"/feedback/{issue.Number}");
            return Results.Ok();
        }

        return Results.Redirect($"/feedback/{issue.Number}");
    }

    private static async Task<IResult> HandleAddCommentAsync(
        HttpContext context,
        int number,
        ICurrentUser user,
        IFeedbackFormPolicy policy,
        IIssueGateway gateway,
        IOptions<StyloIssuesOptions> opts,
        CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.StableId is null)
            return Results.Unauthorized();

        var verdict = policy.Evaluate(context, user);
        // Only FeedbackFormState.Bare (confirmed bot) is hard-blocked here with a 403.
        // FeedbackFormState.ChallengeGated (suspicious, may be human) is intentionally NOT
        // blocked by the core package: challenge handling is a host/bridge responsibility
        // (e.g. redirecting to a CAPTCHA or showing challenge UI before allowing submission).
        if (verdict.State == FeedbackFormState.Bare)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var form = await context.Request.ReadFormAsync(ct);
        var body = form["body"].ToString().Trim();

        if (string.IsNullOrEmpty(body))
            return Results.BadRequest("body is required");

        var marker = ReporterMarker.Compute(Encoding.UTF8.GetBytes(opts.Value.MarkerKey), user.StableId!);

        var reporter = new ReporterContext(user.DisplayName, user.GitHubLogin, marker);
        await gateway.AddCommentAsync(number, body, reporter, ct);

        if (context.Request.Headers.ContainsKey("HX-Request"))
        {
            context.Response.Headers.Append("HX-Redirect", $"/feedback/{number}");
            return Results.Ok();
        }

        return Results.Redirect($"/feedback/{number}");
    }

    private static async Task<IResult> HandleWebhookAsync(HttpContext context, CancellationToken ct)
    {
        var opts = context.RequestServices.GetRequiredService<IOptions<StyloIssuesOptions>>().Value;
        var handler = context.RequestServices.GetRequiredService<WebhookHandler>();

        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms, ct);
        var payload = ms.ToArray();

        var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
        if (!WebhookVerifier.IsValid(payload, signature, opts.WebhookSecret))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
        var doc = JsonDocument.Parse(payload);
        await handler.HandleAsync(eventType, doc.RootElement);

        return Results.Ok();
    }
}
