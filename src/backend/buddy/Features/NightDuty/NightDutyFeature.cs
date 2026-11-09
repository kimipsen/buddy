using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.OpenApi;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.NightDuty;

public static class NightDutyFeature
{
    public const string OpenApiDocumentName = "nightduty";

    private static readonly Type[] EventTypes =
    [
        typeof(NightScheduleStarted),
        typeof(NightPatternReplaced),
        typeof(NightOverridden),
        typeof(NightOverrideCleared)
    ];

    // Depends on IGuardianLinkEventStore (authorization, guardian checks), IBabysitterListEventStore
    // (babysitter checks and names) and IUserEventStore (the child's today), so the Guardians,
    // Babysitters and Users features must be registered first.
    public static IServiceCollection AddNightDutyFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, NightDutyPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, NightDutyPersonalDataExporter>();

        services.AddMartenStore<INightDutyStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "nightduty";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // The two unions persisted in the events and the snapshot need explicit Kind
                    // discriminators -- see NightResponsibleJsonConverter.
                    json.Converters.Add(new NightResponsibleJsonConverter());
                    json.Converters.Add(new NightOverrideJsonConverter());
                });

            // Inline snapshot in the shared "snapshots" schema, registered via Register() for the
            // same reason as WorkLocationsFeature. See docs/backend/analysis/event-stream-snapshots.md.
            options.Projections.Register(new NightScheduleSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<NightScheduleSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<INightScheduleEventStore, MartenNightScheduleEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapNightDutyFeature(this IEndpointRouteBuilder endpoints)
    {
        var nightDuty = endpoints.MapGroup("/night-duty")
            .WithTags("NightDuty")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        nightDuty.MapReplaceNightPattern();
        nightDuty.MapSetNightOverrides();
        nightDuty.MapClearNightOverrides();
        nightDuty.MapGetNightSchedule();
        nightDuty.MapListNights();

        return endpoints;
    }
}
