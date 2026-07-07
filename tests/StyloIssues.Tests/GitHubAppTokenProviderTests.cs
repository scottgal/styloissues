using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class GitHubAppTokenProviderTests
{
    static IOptions<StyloIssuesOptions> Opts() =>
        Options.Create(new StyloIssuesOptions { AppId = 1, InstallationId = 2, PrivateKeyPem = "pem" });

    [Fact]
    public async Task Caches_token_until_near_expiry_then_refetches()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var fetches = 0;
        var sut = new GitHubAppTokenProvider(Opts(), time,
            fetch: (_, _) => { fetches++; return Task.FromResult(("tok" + fetches, time.GetUtcNow().AddHours(1))); });

        Assert.Equal("tok1", await sut.GetInstallationTokenAsync(default));
        Assert.Equal("tok1", await sut.GetInstallationTokenAsync(default)); // cached
        Assert.Equal(1, fetches);

        time.Advance(TimeSpan.FromMinutes(56));                              // within skew of 1h expiry
        Assert.Equal("tok2", await sut.GetInstallationTokenAsync(default));  // refetched
        Assert.Equal(2, fetches);
    }
}
