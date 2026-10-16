using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.OpenApi;
using buddy.Common.Postgres;
using buddy.Features.VisitSummaries;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Feelings;

// The child's feelings check-ins, a guardian's observations, prompt times, the faces-only display
// mode and doctor share links (docs/backend/analysis/feelings-check-in.md).
public static class FeelingsFeature
{
    public const string OpenApiDocumentName = "feelings";

    private static readonly Type[] EventTypes =
    [
        typeof(FeelingsJournalStarted),
        typeof(FeelingCheckedIn),
        typeof(FeelingCheckInEdited),
        typeof(FeelingCheckInRemoved),
        typeof(CheckInPromptTimesSet),
        typeof(FeelingsDisplayModeSet),
        typeof(FeelingsShareTokenCreated),
        typeof(FeelingsShareTokenRevoked)
    ];

    // Depends on IGuardianLinkEventStore for authorization and IUserEventStore for the child's time
    // zone and name, so AddUsersFeature and AddGuardiansFeature must run first.
    public static IServiceCollection AddFeelingsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // "Now" for the undo window and the child's local clock; tests swap in a fake one.
        services.TryAddSingleton(TimeProvider.System);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases and exports this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, FeelingsPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, FeelingsPersonalDataExporter>();

        services.AddMartenStore<IFeelingsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "feelings";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            // CheckIns is keyed by FeelingCheckInId, which the id converter also writes as a
            // property name.
            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshots in the shared "snapshots" schema, registered via Register() rather
            // than Projections.Snapshot<T>() -- see PickupsFeature for why.
            options.Projections.Register(new FeelingsJournalSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<FeelingsJournalSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);
            options.Projections.Register(new FeelingsShareTokenSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<FeelingsShareTokenSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IFeelingsJournalEventStore, MartenFeelingsJournalEventStore>();
        services.AddSingleton<IFeelingsShareTokenEventStore, MartenFeelingsShareTokenEventStore>();
        // The feelings section of a doctor visit summary (Features/VisitSummaries).
        services.AddSingleton<IVisitSummarySectionSource, FeelingsVisitSummarySource>();

        return services;
    }

    public static IEndpointRouteBuilder MapFeelingsFeature(this IEndpointRouteBuilder endpoints)
    {
        var feelings = endpoints.MapGroup("/feelings")
            .WithTags("Feelings")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        // The child's own routes: they can only ever reach the caller's journal.
        feelings.MapCheckIn();
        feelings.MapUndoCheckIn();
        feelings.MapGetMyFeelingsToday();

        // A guardian's routes.
        feelings.MapLogFeelingCheckIn();
        feelings.MapEditFeelingCheckIn();
        feelings.MapRemoveFeelingCheckIn();
        feelings.MapListFeelingCheckIns();
        feelings.MapSetCheckInPromptTimes();
        feelings.MapSetFeelingsDisplayMode();
        feelings.MapCreateFeelingsShareLink();
        feelings.MapListFeelingsShareLinks();
        feelings.MapRevokeFeelingsShareLink();

        // Anonymous, token-gated.
        feelings.MapGetSharedFeelings();

        return endpoints;
    }
}
