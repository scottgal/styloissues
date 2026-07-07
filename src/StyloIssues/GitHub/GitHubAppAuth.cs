using GitHubJwt;
using Octokit;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

/// <summary>
/// Mints a GitHub App installation token using a short-lived App JWT (via GitHubJwt 0.0.6)
/// and exchanges it for an installation access token via the Octokit 14 API.
/// </summary>
public static class GitHubAppAuth
{
    /// <summary>
    /// Real implementation of <see cref="GitHubAppTokenProvider.Fetch"/>.
    /// Matches the delegate signature exactly so it can be passed by method group.
    /// </summary>
    public static async Task<(string token, DateTimeOffset expiry)> FetchInstallationToken(
        StyloIssuesOptions o, CancellationToken ct)
    {
        // Step 1: mint a short-lived App JWT (valid up to 10 minutes; use 9 minutes for skew)
        var jwt = new GitHubJwtFactory(
            new StringPrivateKeySource(o.PrivateKeyPem),
            new GitHubJwtFactoryOptions
            {
                AppIntegrationId = (int)o.AppId,
                ExpirationSeconds = 540
            })
            .CreateEncodedJwtToken();

        // Step 2: build an Octokit client authenticated as the App
        var appClient = new GitHubClient(new ProductHeaderValue("styloissues"))
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer)
        };

        // Step 3: exchange for an installation token
        var result = await appClient.GitHubApps.CreateInstallationToken(o.InstallationId);

        return (result.Token, result.ExpiresAt);
    }
}
