namespace buddy.Features.Privacy;

// Erasure and export across Buddy -- see docs/backend/analysis/gdpr-data-protection.md. Each feature
// registers its own IPersonalDataEraser and IPersonalDataExporter; this feature orchestrates them.
// Registered after every other feature.
public static class PrivacyFeature
{
    public static IServiceCollection AddPrivacyFeature(this IServiceCollection services)
    {
        // Transient: it depends on IKeycloakAdminClient, a typed HttpClient.
        services.AddTransient<UserErasure>();
        services.AddSingleton<PersonalDataExport>();
        services.AddHostedService<UserErasureService>();

        return services;
    }
}
