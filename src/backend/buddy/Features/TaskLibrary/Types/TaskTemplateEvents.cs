using System.Collections.Immutable;
using System.Text.Json.Serialization;

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
    TaskTemplateArchived,
    ChecklistOptionsChanged,
    ChecklistItemSuggested,
    ChecklistItemSuggestionApproved,
    ChecklistItemSuggestionDeclined,
    ChecklistItemSuggestionWithdrawn
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
        ChecklistOptionsChanged e => e,
        ChecklistItemSuggested e => e,
        ChecklistItemSuggestionApproved e => e,
        ChecklistItemSuggestionDeclined e => e,
        ChecklistItemSuggestionWithdrawn e => e,
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
        ChecklistOptionsChanged => nameof(ChecklistOptionsChanged),
        ChecklistItemSuggested => nameof(ChecklistItemSuggested),
        ChecklistItemSuggestionApproved => nameof(ChecklistItemSuggestionApproved),
        ChecklistItemSuggestionDeclined => nameof(ChecklistItemSuggestionDeclined),
        ChecklistItemSuggestionWithdrawn => nameof(ChecklistItemSuggestionWithdrawn),
    };
}

// ChildId records the template's owning child -- needed by MartenTaskTemplateEventStore.CreateAsync
// to seed the template's index row, but not projected onto the TaskTemplate aggregate itself;
// ownership is looked up via the index (ITaskTemplateEventStore.ListIdsForChildAsync/
// FindChildIdForTemplateAsync) rather than carried on aggregate state. Kind is a trailing parameter
// with a default and is left out of the stored JSON while Routine, so events written before
// packing checklists keep their shape and replay as routines.
public sealed record TaskTemplateCreated(
    TaskTemplateId Id,
    UserId ChildId,
    UserId CreatedBy,
    string Name,
    Icon Icon,
    Color Color,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] TaskTemplateKind Kind = TaskTemplateKind.Routine);

public sealed record TaskTemplateDetailsUpdated(TaskTemplateId Id, TaskTemplateDetails Before, TaskTemplateDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskAdded(TaskTemplateId Id, Subtask Subtask, int Position, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskUpdated(TaskTemplateId Id, SubtaskId SubtaskId, Subtask Before, Subtask After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtaskRemoved(TaskTemplateId Id, SubtaskId SubtaskId, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record SubtasksReordered(TaskTemplateId Id, ImmutableList<SubtaskId> After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Soft "delete" -- same shape as MealArchived. An archived template can no longer be newly
// scheduled (Features/Calendars, a later step), but remains fully readable in the library.
public sealed record TaskTemplateArchived(TaskTemplateId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

// A checklist's display options changed (Before/After like TaskTemplateDetailsUpdated). Only ever
// appended to a Checklist template; also appended right after TaskTemplateCreated when a checklist
// is created (or copied) with options.
public sealed record ChecklistOptionsChanged(TaskTemplateId Id, ChecklistOptions Before, ChecklistOptions After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Child suggestions for a packing list (docs/backend/analysis/packing-checklists.md, Question 8).
// None of them changes the list itself or LastModifiedBy; approval is appended together with the
// SubtaskAdded that creates SubtaskId. Title is the child's own text and is masked on erasure.
public sealed record ChecklistItemSuggested(TaskTemplateId Id, ChecklistSuggestionId SuggestionId, string Title, Weekdays Weekdays, UserId SuggestedBy, DateTimeOffset OccurredAt);

public sealed record ChecklistItemSuggestionApproved(TaskTemplateId Id, ChecklistSuggestionId SuggestionId, SubtaskId SubtaskId, UserId ApprovedBy, DateTimeOffset OccurredAt);

// Also appended by ArchiveTaskTemplate for every pending suggestion.
public sealed record ChecklistItemSuggestionDeclined(TaskTemplateId Id, ChecklistSuggestionId SuggestionId, UserId DeclinedBy, DateTimeOffset OccurredAt);

// By the child; also appended by the eraser when the list passes to an heir.
public sealed record ChecklistItemSuggestionWithdrawn(TaskTemplateId Id, ChecklistSuggestionId SuggestionId, UserId WithdrawnBy, DateTimeOffset OccurredAt);
