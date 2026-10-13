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

namespace buddy.Features.TransitionWarnings;

// Per-child settings for the heads-up a child sees before a transition
// (docs/backend/analysis/transition-warnings.md). The server only stores and serves the settings;
// the child's browser computes the warnings from the occurrences and pickups it already loads.
public static class TransitionWarningsFeature
{
    public const string OpenApiDocumentName = "transitionwarnings";

    private static readonly Type[] EventTypes =
    [
        typeof(TransitionWarningsStarted),
        typeof(TransitionWarningDefaultsConfigured),
        typeof(ItemWarningOverrideSet),
        typeof(ItemWarningOverrideCleared)
    ];

    // After Guardians, Groups and Calendars: authorization reads IGuardianLinkEventStore, and the
    // override item check reads ICalendarItemEventStore, ICalendarEventStore and IGroupEventStore.
    public static IServiceCollection AddTransitionWarningsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        services.AddSingleton<IPersonalDataEraser, TransitionWarningsPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, TransitionWarningsPersonalDataExporter>();

        services.AddMartenStore<ITransitionWarningsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "transitionwarnings";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            // ItemOverrides is keyed by CalendarItemId, which the id converter also writes as a
            // property name.
            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshot in the shared "snapshots" schema; Register(), not Snapshot<T>(), for
            // the same reason as ProgressFeature.
            options.Projections.Register(new TransitionWarningSettingsSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<TransitionWarningSettingsSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<ITransitionWarningSettingsEventStore, MartenTransitionWarningSettingsEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapTransitionWarningsFeature(this IEndpointRouteBuilder endpoints)
    {
        var transitionWarnings = endpoints.MapGroup("/transition-warnings")
            .WithTags("TransitionWarnings")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        transitionWarnings.MapGetMyTransitionWarnings();
        transitionWarnings.MapGetChildTransitionWarnings();
        transitionWarnings.MapConfigureTransitionWarnings();
        transitionWarnings.MapSetItemWarningOverride();
        transitionWarnings.MapClearItemWarningOverride();

        return endpoints;
    }
}
