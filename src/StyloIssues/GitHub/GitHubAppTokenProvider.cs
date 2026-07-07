using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider
{
    // Fetch delegate returns (token, absoluteExpiry). Real impl mints an App JWT
    // via GitHubJwt from options.PrivateKeyPem, then POSTs installations/{id}/access_tokens.
    public delegate Task<(string token, DateTimeOffset expiry)> Fetch(StyloIssuesOptions o, CancellationToken ct);

    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);
    private readonly StyloIssuesOptions _o;
    private readonly TimeProvider _time;
    private readonly Fetch _fetch;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiry = DateTimeOffset.MinValue;

    public GitHubAppTokenProvider(IOptions<StyloIssuesOptions> o, TimeProvider time, Fetch fetch)
    { _o = o.Value; _time = time; _fetch = fetch; }

    public async Task<string> GetInstallationTokenAsync(CancellationToken ct)
    {
        if (_token is not null && _time.GetUtcNow() < _expiry - Skew) return _token;
        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && _time.GetUtcNow() < _expiry - Skew) return _token;
            (_token, _expiry) = await _fetch(_o, ct);
            return _token!;
        }
        finally { _gate.Release(); }
    }
}
