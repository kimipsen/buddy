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

namespace buddy.Features.Babysitters;

public static class BabysittersFeature
{
    public const string OpenApiDocumentName = "babysitters";

    private static readonly Type[] EventTypes =
    [
        typeof(BabysitterListStarted),
        typeof(BabysitterAdded),
        typeof(BabysitterDetailsChanged),
        typeof(BabysitterArchived)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first.
    // Pickups reads IBabysitterListEventStore, so this must run before AddPickupsFeature.
    public static IServiceCollection AddBabysittersFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, BabysittersPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, BabysittersPersonalDataExporter>();

        services.AddMartenStore<IBabysittersStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "babysitters";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshot in the shared "snapshots" schema, registered via Register() for the
            // same reason as PickupsFeature. See docs/backend/analysis/event-stream-snapshots.md.
            options.Projections.Register(new BabysitterListSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<BabysitterListSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IBabysitterListEventStore, MartenBabysitterListEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapBabysittersFeature(this IEndpointRouteBuilder endpoints)
    {
        var babysitters = endpoints.MapGroup("/babysitters")
            .WithTags("Babysitters")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        babysitters.MapListMyBabysitters();
        babysitters.MapAddBabysitter();
        babysitters.MapUpdateBabysitter();
        babysitters.MapArchiveBabysitter();
        babysitters.MapListChildBabysitters();

        return endpoints;
    }
}
