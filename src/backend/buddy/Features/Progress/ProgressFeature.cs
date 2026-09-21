using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Microsoft.Extensions.Options;

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

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IProgressStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "progress";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // ChildProgress.AwardedOccurrences is an ImmutableHashSet of 3-element tuples
                    // (CalendarItemId, DateOnly, Guid?). Plain System.Text.Json silently serializes
                    // a ValueTuple set element as "{}" (ItemN are public fields, not properties) --
                    // a real data-loss trap, not a missing feature. This converter fixes it.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());
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
            options.Schema.For<ChildProgressSnapshot>().DatabaseSchemaName("snapshots");

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
            .WithGroupName(OpenApiDocumentName);

        progress.MapGetMyProgress();
        progress.MapGetChildProgress();
        progress.MapConfigureGoalPosts();

        return endpoints;
    }
}
