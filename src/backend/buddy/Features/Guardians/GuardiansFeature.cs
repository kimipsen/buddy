using buddy.Common.Configuration;
using buddy.Common.Erasure;
using buddy.Common.Http;

namespace buddy.Features.Guardians;

public static class GuardiansFeature
{
    public const string OpenApiDocumentName = "guardians";
    private const string Tag = "Guardians";

    // Depends on IUsersStore, so AddUsersFeature must run first -- same DI ordering constraint
    // Groups/Calendars already have relative to Users.
    public static IServiceCollection AddGuardiansFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.AddValidatedOptions<KeycloakAdminOptions>(KeycloakAdminOptions.SectionName);

        services.AddSingleton<IGuardianLinkEventStore, MartenGuardianLinkEventStore>();
        services.AddSingleton<IGuardianInviteEventStore, MartenGuardianInviteEventStore>();
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();
        // Erases guardian links and invites (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, GuardiansPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, GuardiansPersonalDataExporter>();

        return services;
    }

    public static IEndpointRouteBuilder MapGuardiansFeature(this IEndpointRouteBuilder endpoints)
    {
        var children = endpoints.MapGroup("/users/me/children")
            .WithTags(Tag)
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        children.MapCreateChild();
        children.MapListMyChildren();
        children.MapListChildGuardians();
        children.MapRevokeGuardianLink();
        children.MapDeleteChild();
        children.MapResetChildPassword();
        children.MapUpdateChildLanguage();
        children.MapUpdateChildTimeZone();
        children.MapInviteGuardian();
        children.MapListGuardianInvites();
        children.MapRevokeGuardianInvite();

        var guardians = endpoints.MapGroup("/users/me/guardians")
            .WithTags(Tag)
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        guardians.MapListMyGuardians();

        var siblings = endpoints.MapGroup("/users/me/siblings")
            .WithTags(Tag)
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        siblings.MapListMySiblings();

        // A separate route group, the same reason Groups splits off "/invites": PreviewGuardianInvite
        // must stay reachable by an unauthenticated caller who only has the token from an email
        // link, while AcceptGuardianInvite needs auth applied only to itself.
        var guardianInvites = endpoints.MapGroup("/guardian-invites")
            .WithTags(Tag)
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        guardianInvites.MapPreviewGuardianInvite();
        guardianInvites.MapAcceptGuardianInvite();

        return endpoints;
    }
}
