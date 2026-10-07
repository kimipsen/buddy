using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Pickups;

public static class PickupsFeature
{
    public const string OpenApiDocumentName = "pickups";

    private static readonly Type[] EventTypes =
    [
        typeof(PickupScheduleCreated),
        typeof(PickupAssigned),
        typeof(PickupCleared)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars/Medicines/Mealplans already have relative to
    // Guardians.
    public static IServiceCollection AddPickupsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, PickupsPersonalDataEraser>();

        services.AddMartenStore<IPickupsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "pickups";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());
                    // PickupSchedule.Assignments is keyed by a ValueTuple (DateOnly, PickupSlot),
                    // which plain System.Text.Json can't (de)serialize -- see
                    // docs/backend/analysis/event-stream-snapshots.md, Question 4/5. Marten's
                    // serializer options here are separate from Program.cs's, so this needs its
                    // own registration.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());

                    // PickupAssignee's cases need an explicit Kind discriminator to round-trip (see
                    // PickupAssigneeJsonConverter).
                    json.Converters.Add(new PickupAssigneeJsonConverter());
                });

            // Inline snapshot of PickupSchedule, kept transactionally consistent with every event
            // append. Routed to a schema separate from "pickups" -- it's derived/rebuildable read
            // state, never the source of truth. See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for PickupScheduleSnapshot's
            // Guid Id. Register() takes the already-typed PickupScheduleSnapshotProjection
            // instance directly, sidestepping that lookup.
            options.Projections.Register(new PickupScheduleSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<PickupScheduleSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IPickupScheduleEventStore, MartenPickupScheduleEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapPickupsFeature(this IEndpointRouteBuilder endpoints)
    {
        var pickups = endpoints.MapGroup("/pickups")
            .WithTags("Pickups")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        pickups.MapAssignPickup();
        pickups.MapClearPickup();
        pickups.MapListPickupSchedule();

        return endpoints;
    }
}
