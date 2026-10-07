using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Features.Calendars;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Progress;

// RecordStarChange (see that folder) has no endpoint of its own -- it's only ever called
// explicitly from other features' handlers, never over HTTP. GetMyProgress/GetChildProgress are
// the read endpoints the dashboards need; ConfigureGoalPosts is Progress's first guardian write
// (see docs/backend/analysis/configurable-goal-posts.md).
public static class ProgressFeature
{
    public const string OpenApiDocumentName = "progress";

    private static readonly Type[] EventTypes =
    [
        typeof(ProgressStarted),
        typeof(StarAwarded),
        typeof(StarRevoked),
        typeof(MilestoneUnlocked),
        typeof(GoalPostsConfigured)
    ];

    public static IServiceCollection AddProgressFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, ProgressPersonalDataEraser>();

        services.AddMartenStore<IProgressStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "progress";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // StarAwarded/StarRevoked and ChildProgress.AwardedOccurrences carry Calendars'
                    // CompletionTarget -- see CompletionTargetJsonConverter.
                    json.Converters.Add(new CompletionTargetJsonConverter());
                });

            // Inline snapshot of ChildProgress, kept transactionally consistent with every event
            // append. Routed to a schema separate from "progress" -- it's derived/rebuildable read
            // state, never the source of truth. See docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for ChildProgressSnapshot's
            // Guid Id. Register() takes the already-typed ChildProgressSnapshotProjection instance
            // directly, sidestepping that lookup.
            options.Projections.Register(new ChildProgressSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<ChildProgressSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IProgressEventStore, MartenProgressEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapProgressFeature(this IEndpointRouteBuilder endpoints)
    {
        var progress = endpoints.MapGroup("/progress")
            .WithTags("Progress")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        progress.MapGetMyProgress();
        progress.MapGetChildProgress();
        progress.MapConfigureGoalPosts();

        return endpoints;
    }
}
