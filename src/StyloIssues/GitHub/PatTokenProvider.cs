using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

/// <summary>
/// Token provider that returns the configured Personal Access Token directly,
/// skipping the GitHub App JWT and installation token flow.
/// </summary>
public sealed class PatTokenProvider : IGitHubAppTokenProvider
{
    private readonly IOptions<StyloIssuesOptions> _options;

    public PatTokenProvider(IOptions<StyloIssuesOptions> options)
    {
        _options = options;
    }

    public Task<string> GetInstallationTokenAsync(CancellationToken ct)
        => Task.FromResult(_options.Value.PersonalAccessToken);
}
