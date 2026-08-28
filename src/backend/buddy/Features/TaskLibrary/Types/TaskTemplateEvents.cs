using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public union TaskTemplateEvent(
    TaskTemplateCreated,
    TaskTemplateDetailsUpdated,
    SubtaskAdded,
    SubtaskUpdated,
    SubtaskRemoved,
    SubtasksReordered,
    TaskTemplateArchived
)
{
    public static TaskTemplateEvent FromPayload(object payload) => payload switch
    {
        TaskTemplateCreated e => e,
        TaskTemplateDetailsUpdated e => e,
        SubtaskAdded e => e,
        SubtaskUpdated e => e,
        SubtaskRemoved e => e,
        SubtasksReordered e => e,
        TaskTemplateArchived e => e,
        _ => throw new ArgumentException($"Unknown task template event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        TaskTemplateCreated => nameof(TaskTemplateCreated),
        TaskTemplateDetailsUpdated => nameof(TaskTemplateDetailsUpdated),
        SubtaskAdded => nameof(SubtaskAdded),
        SubtaskUpdated => nameof(SubtaskUpdated),
        SubtaskRemoved => nameof(SubtaskRemoved),
        SubtasksReordered => nameof(SubtasksReordered),
        TaskTemplateArchived => nameof(TaskTemplateArchived),
    };
}

// ChildId records the template's owning child -- needed by MartenTaskTemplateEventStore.CreateAsync
// to seed the template's index row, but not projected onto the TaskTemplate aggregate itself;
// ownership is looked up via the index (ITaskTemplateEventStore.ListIdsForChildAsync/
// FindChildIdForTemplateAsync) rather than carried on aggregate state.
public sealed record TaskTemplateCreated(
    TaskTemplateId Id, UserId ChildId, UserId CreatedBy, string Name, Icon Icon, Color Color, DateTimeOffset OccurredAt);

public sealed record TaskTemplateDetailsUpdated(TaskTemplateId Id, TaskTemplateDetails Before, TaskTemplateDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskAdded(TaskTemplateId Id, Subtask Subtask, int Position, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskUpdated(TaskTemplateId Id, SubtaskId SubtaskId, Subtask Before, Subtask After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskRemoved(TaskTemplateId Id, SubtaskId SubtaskId, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtasksReordered(TaskTemplateId Id, ImmutableList<SubtaskId> After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Soft "delete" -- same shape as MealArchived. An archived template can no longer be newly
// scheduled (Features/Calendars, a later step), but remains fully readable in the library.
public sealed record TaskTemplateArchived(TaskTemplateId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);
