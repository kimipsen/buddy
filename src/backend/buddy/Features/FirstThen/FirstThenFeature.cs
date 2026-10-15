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

namespace buddy.Features.FirstThen;

public static class FirstThenFeature
{
    public const string OpenApiDocumentName = "firstthen";

    private static readonly Type[] EventTypes =
    [
        typeof(FirstThenBoardStarted),
        typeof(FirstThenBoardSet),
        typeof(FirstThenFirstCompleted),
        typeof(FirstThenFirstReopened),
        typeof(FirstThenBoardEnded)
    ];

    // Depends on IGuardianLinkEventStore for authorization and IUserEventStore for the last
    // modifier's name, so AddUsersFeature and AddGuardiansFeature must run first.
    public static IServiceCollection AddFirstThenFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, FirstThenPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, FirstThenPersonalDataExporter>();

        services.AddMartenStore<IFirstThenStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "firstthen";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshots in the shared "snapshots" schema, registered via Register() rather
            // than Projections.Snapshot<T>() -- see PickupsFeature for why.
            options.Projections.Register(new FirstThenBoardSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<FirstThenBoardSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IFirstThenBoardEventStore, MartenFirstThenBoardEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapFirstThenFeature(this IEndpointRouteBuilder endpoints)
    {
        var firstThen = endpoints.MapGroup("/first-then")
            .WithTags("FirstThen")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        // The child's own routes: they can only ever reach the caller's board.
        firstThen.MapGetMyFirstThenBoard();

        // A guardian's routes.
        firstThen.MapGetFirstThenBoard();
        firstThen.MapSetFirstThenBoard();

        // Both route families.
        firstThen.MapCompleteFirstThenFirst();
        firstThen.MapReopenFirstThenFirst();
        firstThen.MapEndFirstThenBoard();

        return endpoints;
    }
}
