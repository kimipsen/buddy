using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.WorkLocations;

public static class WorkLocationsFeature
{
    public const string OpenApiDocumentName = "worklocations";

    private static readonly Type[] EventTypes =
    [
        typeof(WorkLocationScheduleStarted),
        typeof(WorkLocationAdded),
        typeof(WorkLocationDetailsChanged),
        typeof(WorkLocationArchived),
        typeof(WorkPatternReplaced),
        typeof(WorkLocationOverridden),
        typeof(WorkLocationOverrideCleared)
    ];

    // Depends on IGuardianLinkEventStore for authorization (co-guardian checks), so
    // AddGuardiansFeature must run first -- same DI ordering constraint Pickups has.
    public static IServiceCollection AddWorkLocationsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddMartenStore<IWorkLocationsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "worklocations";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // WorkDayOverride (the override events, WorkLocationSchedule.Overrides) needs an
                    // explicit Kind discriminator -- see WorkDayOverrideJsonConverter.
                    json.Converters.Add(new WorkDayOverrideJsonConverter());
                });

            // Inline snapshot in the shared "snapshots" schema, registered via Register() for the
            // same reason as PickupsFeature (Projections.Snapshot<T>() throws for a Guid-wrapper
            // document). See docs/backend/analysis/event-stream-snapshots.md.
            options.Projections.Register(new WorkLocationScheduleSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<WorkLocationScheduleSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IWorkLocationScheduleEventStore, MartenWorkLocationScheduleEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapWorkLocationsFeature(this IEndpointRouteBuilder endpoints)
    {
        var workLocations = endpoints.MapGroup("/work-locations")
            .WithTags("WorkLocations")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        workLocations.MapAddWorkLocation();
        workLocations.MapUpdateWorkLocation();
        workLocations.MapArchiveWorkLocation();
        workLocations.MapReplaceWorkPattern();
        workLocations.MapSetWorkLocationOverrides();
        workLocations.MapClearWorkLocationOverrides();
        workLocations.MapGetWorkLocationSchedule();
        workLocations.MapListWorkDays();

        return endpoints;
    }
}
