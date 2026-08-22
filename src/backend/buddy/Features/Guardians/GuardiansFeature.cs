using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class GuardiansFeature
{
    public const string OpenApiDocumentName = "guardians";

    // Depends on IUsersStore, so AddUsersFeature must run first -- same DI ordering constraint
    // Groups/Calendars already have relative to Users.
    public static IServiceCollection AddGuardiansFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.Configure<KeycloakAdminOptions>(configuration.GetSection(KeycloakAdminOptions.SectionName));

        services.AddSingleton<IGuardianLinkEventStore, MartenGuardianLinkEventStore>();
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }

    public static IEndpointRouteBuilder MapGuardiansFeature(this IEndpointRouteBuilder endpoints)
    {
        var children = endpoints.MapGroup("/users/me/children")
            .WithTags("Guardians")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        children.MapCreateChild();
        children.MapListMyChildren();
        children.MapRevokeGuardianLink();

        var guardians = endpoints.MapGroup("/users/me/guardians")
            .WithTags("Guardians")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        guardians.MapListMyGuardians();

        return endpoints;
    }
}
