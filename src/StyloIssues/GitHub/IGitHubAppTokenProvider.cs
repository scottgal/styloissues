namespace StyloIssues.GitHub;

public interface IGitHubAppTokenProvider
{
    Task<string> GetInstallationTokenAsync(CancellationToken ct);
}
