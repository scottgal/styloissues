using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.GitHub;

public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider, IDisposable
{
    // Fetch delegate returns (token, absoluteExpiry). Real impl mints an App JWT
    // via GitHubJwt from options.PrivateKeyPem, then POSTs installations/{id}/access_tokens.
    public delegate Task<(string token, DateTimeOffset expiry)> Fetch(StyloIssuesOptions o, CancellationToken ct);

    private sealed record CachedToken(string Token, DateTimeOffset Expiry);

    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);
    private readonly StyloIssuesOptions _o;
    private readonly TimeProvider _time;
    private readonly Fetch _fetch;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CachedToken? _cached;

    public GitHubAppTokenProvider(IOptions<StyloIssuesOptions> o, TimeProvider time, Fetch fetch)
    { _o = o.Value; _time = time; _fetch = fetch; }

    public async Task<string> GetInstallationTokenAsync(CancellationToken ct)
    {
        var c = _cached;
        if (c is not null && _time.GetUtcNow() < c.Expiry - Skew) return c.Token;
        await _gate.WaitAsync(ct);
        try
        {
            c = _cached;
            if (c is not null && _time.GetUtcNow() < c.Expiry - Skew) return c.Token;
            var (token, expiry) = await _fetch(_o, ct);
            _cached = new CachedToken(token, expiry);
            return token;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
