using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.TaskLibrary;

public static class TaskLibraryFeature
{
    public const string OpenApiDocumentName = "tasklibrary";

    private static readonly Type[] EventTypes =
    [
        typeof(TaskTemplateCreated),
        typeof(TaskTemplateDetailsUpdated),
        typeof(SubtaskAdded),
        typeof(SubtaskUpdated),
        typeof(SubtaskRemoved),
        typeof(SubtasksReordered),
        typeof(TaskTemplateArchived)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Mealplans/Calendars/Medicines already have relative to
    // Guardians.
    public static IServiceCollection AddTaskLibraryFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddMartenStore<ITaskLibraryStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "tasklibrary";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshot of TaskTemplate, kept transactionally consistent with every event
            // append. Routed to a schema separate from "tasklibrary" -- it's derived/rebuildable
            // read state, never the source of truth. See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for TaskTemplateSnapshot's
            // Guid Id. Register() takes the already-typed TaskTemplateSnapshotProjection instance
            // directly, sidestepping that lookup.
            options.Projections.Register(new TaskTemplateSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<TaskTemplateSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<ITaskTemplateEventStore, MartenTaskTemplateEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapTaskLibraryFeature(this IEndpointRouteBuilder endpoints)
    {
        var taskTemplates = endpoints.MapGroup("/task-templates")
            .WithTags("TaskLibrary")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        taskTemplates.MapCreateTaskTemplate();
        taskTemplates.MapUpdateTaskTemplate();
        taskTemplates.MapArchiveTaskTemplate();
        taskTemplates.MapListTaskTemplates();

        taskTemplates.MapAddSubtask();
        taskTemplates.MapUpdateSubtask();
        taskTemplates.MapRemoveSubtask();
        taskTemplates.MapReorderSubtasks();

        return endpoints;
    }
}
