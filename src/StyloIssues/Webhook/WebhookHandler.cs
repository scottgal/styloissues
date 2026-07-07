using System.Text.Json;
using StyloIssues;

namespace StyloIssues.Webhook;

public sealed class WebhookHandler
{
    private readonly IIssueReader _reader;
    public WebhookHandler(IIssueReader reader) => _reader = reader;

    public Task HandleAsync(string eventType, JsonElement payload)
    {
        if (eventType is "issues" or "issue_comment"
            && payload.TryGetProperty("issue", out var issue)
            && issue.TryGetProperty("number", out var n) && n.TryGetInt32(out var number))
        {
            _reader.Invalidate(number);
        }
        return Task.CompletedTask;
    }
}
