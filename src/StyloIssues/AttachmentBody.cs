using StyloIssues.Abstractions;

namespace StyloIssues;

/// <summary>
/// Builds the GitHub issue body by appending attachment metadata when present.
/// </summary>
public static class AttachmentBody
{
    /// <summary>
    /// Returns <paramref name="body"/> unchanged when both Url and ManifestSummary are absent.
    /// Otherwise appends a link line and/or a collapsed details block.
    /// </summary>
    public static string Append(string body, IssueAttachment a)
    {
        var b = body;
        if (!string.IsNullOrEmpty(a.Url))
            b += $"\n\n**Diagnostic archive:** [{a.FileName}]({a.Url})";
        if (!string.IsNullOrEmpty(a.ManifestSummary))
            b += $"\n\n<details><summary>Detection snapshot manifest</summary>\n\n```json\n{a.ManifestSummary}\n```\n\n</details>";
        return b;
    }
}
