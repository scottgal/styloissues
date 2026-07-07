using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class PatTokenProviderTests
{
    static IOptions<StyloIssuesOptions> Opts(string pat = "") =>
        Options.Create(new StyloIssuesOptions
        {
            PersonalAccessToken = pat,
            AppId = 1,
            InstallationId = 2,
            PrivateKeyPem = "pem"
        });

    [Fact]
    public async Task Returns_configured_pat_directly()
    {
        var sut = new PatTokenProvider(Opts("ghp_test_token"));
        var token = await sut.GetInstallationTokenAsync(default);
        Assert.Equal("ghp_test_token", token);
    }

    [Fact]
    public void DI_resolves_PatTokenProvider_when_pat_is_set()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddStyloIssues(o =>
        {
            o.PersonalAccessToken = "ghp_test_token";
            o.AppId = 1;
            o.InstallationId = 2;
            o.PrivateKeyPem = "pem";
        });

        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IGitHubAppTokenProvider>();
        Assert.IsType<PatTokenProvider>(provider);
    }

    [Fact]
    public void DI_resolves_GitHubAppTokenProvider_when_pat_is_empty()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddStyloIssues(o =>
        {
            o.PersonalAccessToken = "";
            o.AppId = 1;
            o.InstallationId = 2;
            o.PrivateKeyPem = "pem";
        });

        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IGitHubAppTokenProvider>();
        Assert.IsType<GitHubAppTokenProvider>(provider);
    }
}
