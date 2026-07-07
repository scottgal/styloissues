using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Octokit;
using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using StyloIssues.Sync;
using StyloIssues.Webhook;

namespace StyloIssues;

public static class StyloIssuesServiceCollectionExtensions
{
    public static IServiceCollection AddStyloIssues(
        this IServiceCollection services,
        Action<StyloIssuesOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IGitHubAppTokenProvider>(sp => new GitHubAppTokenProvider(
            sp.GetRequiredService<IOptions<StyloIssuesOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            GitHubAppAuth.FetchInstallationToken));

        services.AddSingleton<Func<string, IGitHubClient>>(_ => token =>
            new GitHubClient(new ProductHeaderValue("styloissues"))
            {
                Credentials = new Credentials(token)
            });

        services.AddSingleton<IIssueGateway, OctokitIssueGateway>();
        services.AddSingleton<IIssueReader, CachingIssueReader>();
        services.AddSingleton<WebhookHandler>();
        services.AddHostedService<ReconcilerService>();

        services.TryAddSingleton<IFeedbackFormPolicy, DefaultFeedbackFormPolicy>();
        services.TryAddSingleton<IIssueStore, NullIssueStore>();
        services.TryAddSingleton<IIssueAttachmentSource, NullIssueAttachmentSource>();

        return services;
    }
}
