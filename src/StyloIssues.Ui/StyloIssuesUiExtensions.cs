using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace StyloIssues.Ui;

public static class StyloIssuesUiExtensions
{
    /// <summary>
    /// Registers MVC with the StyloIssues.Ui assembly part (controller + views).
    /// The host application is responsible for registering ICurrentUser.
    /// </summary>
    public static IServiceCollection AddStyloIssuesUi(this IServiceCollection services)
    {
        services.AddControllersWithViews()
                .AddApplicationPart(typeof(FeedbackController).Assembly);
        return services;
    }

    /// <summary>Maps the feedback controller routes.</summary>
    public static IEndpointRouteBuilder MapStyloIssues(this IEndpointRouteBuilder app)
    {
        app.MapControllers();
        return app;
    }
}
