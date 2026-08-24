using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using buddy.Features.Users;

using Weasel.Core;

namespace buddy.Features.Groups;

public static class GroupsFeature
{
    public const string OpenApiDocumentName = "groups";

    private static readonly Type[] EventTypes =
    [
        typeof(GroupCreated),
        typeof(GroupMemberRoleGranted),
        typeof(GroupMemberRoleRevoked),
        typeof(GroupCalendarPolicyUpdated),
        typeof(GroupMealplanPolicyUpdated),
        typeof(GroupMedicinePolicyUpdated),
        typeof(GroupDeleted),
        typeof(GroupInviteCreated),
        typeof(GroupInviteAccepted),
        typeof(GroupInviteRevoked)
    ];

    public static IServiceCollection AddGroupsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IGroupsStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "groups";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<IGroupEventStore, MartenGroupEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapGroupsFeature(this IEndpointRouteBuilder endpoints)
    {
        var groups = endpoints.MapGroup("/groups")
            .WithTags("Groups")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        groups.MapCreateGroup();
        groups.MapGetGroup();
        groups.MapListGroups();
        groups.MapSetGroupMemberRole();
        groups.MapRemoveGroupMember();
        groups.MapAddChildToGroup();
        groups.MapUpdateCalendarPermissionPolicy();
        groups.MapUpdateMealplanPermissionPolicy();
        groups.MapUpdateMedicinePermissionPolicy();
        groups.MapDeleteGroup();
        groups.MapInviteToGroup();
        groups.MapListGroupInvites();
        groups.MapRevokeGroupInvite();

        // A separate route group: PreviewGroupInvite must stay reachable by an unauthenticated
        // caller who has only the token from an email link (so the app can show "You've been
        // invited to X" before forcing a login), while AcceptGroupInvite needs auth applied only
        // to itself rather than inheriting "/groups"'s blanket RequireAuthorization().
        var invites = endpoints.MapGroup("/invites")
            .WithTags("Groups")
            .WithGroupName(OpenApiDocumentName);

        invites.MapPreviewGroupInvite();
        invites.MapAcceptGroupInvite();

        return endpoints;
    }
}
