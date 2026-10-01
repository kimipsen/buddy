using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;


using Npgsql;

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

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddMartenStore<IGroupsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "groups";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshot of Group, kept transactionally consistent with every event append.
            // Routed to a schema separate from "groups" -- it's derived/rebuildable read state,
            // never the source of truth. See docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for GroupSnapshot's Guid Id.
            // Register() takes the already-typed GroupSnapshotProjection instance directly,
            // sidestepping that lookup.
            options.Projections.Register(new GroupSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<GroupSnapshot>().DatabaseSchemaName("snapshots");

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
