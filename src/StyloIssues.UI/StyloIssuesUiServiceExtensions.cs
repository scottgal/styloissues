using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StyloIssues.Abstractions;

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

        // Ensure endpoint dependencies are resolvable even when AddStyloIssues is not called.
        // Hosts that call AddStyloIssues already have these; TryAdd is a no-op in that case.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIssueAttachmentSource, StyloIssues.NullIssueAttachmentSource>();

        // Register antiforgery: form POST endpoints validate the token in-handler.
        // HeaderName matches the hx-headers attribute used by the HTMX path in the ViewComponent views.
        services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

        return services;
    }
}

/// <summary>Marker type used for assembly discovery via AddApplicationPart.</summary>
internal sealed class StyloIssuesUiMarker { }
