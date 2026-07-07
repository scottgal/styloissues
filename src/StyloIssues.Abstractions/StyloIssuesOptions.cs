namespace StyloIssues.Abstractions;

public sealed class StyloIssuesOptions
{
    public const string SectionName = "StyloIssues";

    public string RepoOwner { get; set; } = "";
    public string RepoName { get; set; } = "";
    public long AppId { get; set; }
    public long InstallationId { get; set; }
    public string PrivateKeyPem { get; set; } = "";   // bound from env/secret store
    public string WebhookSecret { get; set; } = "";   // bound from env/secret store
    public string MarkerKey { get; set; } = "";       // bound from env/secret store
    /// <summary>
    /// When set, StyloIssues authenticates to GitHub with this token directly and skips
    /// the GitHub App JWT/installation-token flow (simple single-repo / self-host auth).
    /// When empty, the App flow (AppId/InstallationId/PrivateKeyPem) is used.
    /// </summary>
    public string PersonalAccessToken { get; set; } = "";
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(2);
    public bool EnablePublicList { get; set; } = true;
    public Dictionary<string, string> CategoryLabels { get; set; } = new();
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromMinutes(10);
}
