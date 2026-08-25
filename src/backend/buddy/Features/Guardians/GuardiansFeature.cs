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
        services.AddSingleton<IGuardianInviteEventStore, MartenGuardianInviteEventStore>();
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
        children.MapListChildGuardians();
        children.MapRevokeGuardianLink();
        children.MapUpdateChildLanguage();
        children.MapInviteGuardian();
        children.MapListGuardianInvites();
        children.MapRevokeGuardianInvite();

        var guardians = endpoints.MapGroup("/users/me/guardians")
            .WithTags("Guardians")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        guardians.MapListMyGuardians();

        // A separate route group, the same reason Groups splits off "/invites": PreviewGuardianInvite
        // must stay reachable by an unauthenticated caller who only has the token from an email
        // link, while AcceptGuardianInvite needs auth applied only to itself.
        var guardianInvites = endpoints.MapGroup("/guardian-invites")
            .WithTags("Guardians")
            .WithGroupName(OpenApiDocumentName);

        guardianInvites.MapPreviewGuardianInvite();
        guardianInvites.MapAcceptGuardianInvite();

        return endpoints;
    }
}
