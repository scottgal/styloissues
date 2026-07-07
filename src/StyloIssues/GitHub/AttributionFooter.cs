using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public static class AttributionFooter
{
    public static string Build(ReporterContext r)
    {
        var who = r.GitHubLogin is { Length: > 0 } gh ? $"{r.DisplayName} (@{gh})" : r.DisplayName;
        return $"\n\n---\n_Filed via stylo.bot on behalf of {who}._";
    }
}
