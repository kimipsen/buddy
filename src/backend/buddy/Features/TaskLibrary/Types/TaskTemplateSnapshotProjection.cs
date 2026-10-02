using Marten.Events.Aggregation;

namespace buddy.Features.TaskLibrary;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct GroupId(Guid Value)" -- as a document's Id.
// TaskTemplateId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// TaskTemplate itself; this thin wrapper carries the plain Guid Marten needs alongside the actual
// TaskTemplate value. TaskTemplateId stays untouched everywhere else in the codebase -- this
// wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record TaskTemplateSnapshot(Guid Id, TaskTemplate TaskTemplate);

// Inline snapshot of TaskTemplate, maintained by Marten in the same transaction as every event
// append (see TaskLibraryFeature.AddTaskLibraryFeature: options.Projections.Register(new
// TaskTemplateSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "tasklibrary" event schema -- it is derived, rebuildable state, not a second source of truth.
public sealed class TaskTemplateSnapshotProjection : SingleStreamProjection<TaskTemplateSnapshot, Guid>
{
    public static TaskTemplateSnapshot Create(TaskTemplateCreated created) =>
        new(created.Id.Value, TaskTemplate.Start(TaskTemplateEvent.FromPayload(created)));

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, TaskTemplateDetailsUpdated updated) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(updated)) };

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, SubtaskAdded added) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(added)) };

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, SubtaskUpdated updated) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(updated)) };

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, SubtaskRemoved removed) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(removed)) };

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, SubtasksReordered reordered) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(reordered)) };

    public TaskTemplateSnapshot Apply(TaskTemplateSnapshot current, TaskTemplateArchived archived) =>
        current with { TaskTemplate = TaskTemplate.Advance(current.TaskTemplate, TaskTemplateEvent.FromPayload(archived)) };
}
