using buddy.Common.Configuration;

using Microsoft.Extensions.Options;

namespace buddy.Common.FeatureFlags;

// Per-installation feature flags (docs/backend/analysis/feature-flags.md). A disabled feature's
// endpoints are never mapped, so they answer routing's 404; GET /features tells the frontend which
// screens to show.
public static class FeatureFlagsFeature
{
    public static IServiceCollection AddFeatureFlagsFeature(this IServiceCollection services)
    {
        // The binder ignores unknown keys by default, so Features__Medecines=false would leave
        // medicines on without a word. Failing startup names the key instead.
        services.AddValidatedOptions<FeatureOptions>(FeatureOptions.SectionName, binder => binder.ErrorOnUnknownConfiguration = true);

        return services;
    }

    // Read from DI after Build(), so the integration tests' ConfigurationOverride is what counts.
    public static InstallationFeatures GetFeatureFlags(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;

        if (options.SubFlagsOverriddenByParent() is { Count: > 0 } overridden)
        {
            app.Services.GetRequiredService<ILogger<FeatureOptions>>()
                .SubFlagsFollowParent(overridden);
        }

        return options.Effective();
    }

    // Anonymous like /version: the shared sleep diary page needs it before anyone signs in, and
    // which features an installation offers is no secret -- calling the routes reveals the same.
    public static IEndpointRouteBuilder MapFeatureFlags(this IEndpointRouteBuilder app, InstallationFeatures flags)
    {
        app.MapGet("/features", () => TypedResults.Ok(flags))
            .WithName("GetFeatures")
            .WithTags("Features")
            .AllowAnonymous();

        return app;
    }
}
