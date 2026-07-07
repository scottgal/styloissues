using Microsoft.Extensions.DependencyInjection;

namespace StyloIssues.UI;

public static class StyloIssuesUiServiceExtensions
{
    /// <summary>
    /// Registers the StyloIssues UI: ViewComponents, TagHelpers, and endpoint handlers.
    /// The assembly is added as an ApplicationPart so the host MVC finds all components.
    /// Call <see cref="StyloIssuesEndpoints.MapStyloIssues"/> on the endpoint builder to wire routes.
    /// </summary>
    public static IServiceCollection AddStyloIssuesUi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(StyloIssuesUiMarker).Assembly);
        return services;
    }
}

/// <summary>Marker type used for assembly discovery via AddApplicationPart.</summary>
internal sealed class StyloIssuesUiMarker { }
