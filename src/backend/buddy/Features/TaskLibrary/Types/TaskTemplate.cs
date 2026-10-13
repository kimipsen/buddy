using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// No ChildId on the aggregate itself: each template is owned by exactly one child, but that
// ownership lives in the index row written at creation (TaskTemplateIndexDocument), not in the
// event stream -- see ITaskTemplateEventStore.ListIdsForChildAsync/FindChildIdForTemplateAsync.
public sealed record TaskTemplate(
    TaskTemplateId Id,
    UserId CreatedBy,
    string Name,
    Icon Icon,
    Color Color,
    ImmutableList<Subtask> Subtasks,
    bool IsArchived,
    UserId LastModifiedBy,
    TaskTemplateKind Kind,
    ChecklistOptions Checklist,
    // Child suggestions for a checklist, oldest first; always empty for a routine.
    ImmutableList<ChecklistSuggestion> Suggestions)
{
    // Snapshots written before packing checklists have no Checklist/Suggestions; System.Text.Json
    // then passes null. Read those as "no options" and "no suggestions" (a missing Kind is already
    // Routine, the enum's default).
    public ChecklistOptions Checklist { get; init; } = Checklist ?? ChecklistOptions.None;

    public ImmutableList<ChecklistSuggestion> Suggestions { get; init; } = Suggestions ?? [];

    public TimeSpan TotalDuration => Subtasks.Aggregate(TimeSpan.Zero, (sum, subtask) => sum + subtask.Duration);

    public static TaskTemplate? Rehydrate(IEnumerable<TaskTemplateEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static TaskTemplate Replay(IEnumerable<TaskTemplateEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // TaskTemplateSnapshotProjection can drive the same logic one Marten-delivered event at a time
    // instead of duplicating this switch. Deliberately not named Apply/Create -- those names are a
    // convention JasperFx's projection source generator scans for on any type used as a projection
    // document, and TaskTemplate is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static TaskTemplate Start(TaskTemplateEvent @event) => @event switch
    {
        TaskTemplateCreated created => new TaskTemplate(
            created.Id,
            created.CreatedBy,
            created.Name,
            created.Icon,
            created.Color,
            ImmutableList<Subtask>.Empty,
            IsArchived: false,
            created.CreatedBy,
            created.Kind,
            ChecklistOptions.None,
            ImmutableList<ChecklistSuggestion>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(TaskTemplate), @event.EventType)
    };

    public static TaskTemplate Advance(TaskTemplate template, TaskTemplateEvent @event) => @event switch
    {
        TaskTemplateDetailsUpdated updated => template with
        {
            Name = updated.After.Name,
            Icon = updated.After.Icon,
            Color = updated.After.Color,
            LastModifiedBy = updated.ModifiedBy
        },
        SubtaskAdded added => template with
        {
            Subtasks = template.Subtasks.Insert(Math.Clamp(added.Position, 0, template.Subtasks.Count), added.Subtask),
            LastModifiedBy = added.ModifiedBy
        },
        SubtaskUpdated updated => template with
        {
            Subtasks = template.Subtasks.SetItem(
                template.Subtasks.FindIndex(s => s.Id == updated.SubtaskId),
                updated.After),
            LastModifiedBy = updated.ModifiedBy
        },
        SubtaskRemoved removed => template with
        {
            Subtasks = template.Subtasks.RemoveAll(s => s.Id == removed.SubtaskId),
            LastModifiedBy = removed.ModifiedBy
        },
        SubtasksReordered reordered => template with
        {
            Subtasks = Reorder(template.Subtasks, reordered.After),
            LastModifiedBy = reordered.ModifiedBy
        },
        TaskTemplateArchived archived => template with { IsArchived = true, LastModifiedBy = archived.ModifiedBy },
        ChecklistOptionsChanged changed => template with { Checklist = changed.After, LastModifiedBy = changed.ModifiedBy },
        ChecklistItemSuggested suggested => template with
        {
            Suggestions = template.Suggestions.Add(new ChecklistSuggestion(
                suggested.SuggestionId,
                suggested.Title,
                suggested.Weekdays,
                suggested.SuggestedBy,
                ChecklistSuggestionStatus.Pending,
                suggested.OccurredAt,
                ResolvedAt: null,
                ResolvedBy: null,
                SubtaskId: null))
        },
        ChecklistItemSuggestionApproved approved => Resolve(
            template, approved.SuggestionId, s => s with
            {
                Status = ChecklistSuggestionStatus.Approved,
                ResolvedAt = approved.OccurredAt,
                ResolvedBy = approved.ApprovedBy,
                SubtaskId = approved.SubtaskId
            }),
        ChecklistItemSuggestionDeclined declined => Resolve(
            template, declined.SuggestionId, s => s with
            {
                Status = ChecklistSuggestionStatus.Declined,
                ResolvedAt = declined.OccurredAt,
                ResolvedBy = declined.DeclinedBy
            }),
        ChecklistItemSuggestionWithdrawn withdrawn => Resolve(
            template, withdrawn.SuggestionId, s => s with
            {
                Status = ChecklistSuggestionStatus.Withdrawn,
                ResolvedAt = withdrawn.OccurredAt,
                ResolvedBy = withdrawn.WithdrawnBy
            }),
        TaskTemplateCreated => throw EventReplay.AlreadyStarted(nameof(TaskTemplate), @event.EventType)
    };

    public ChecklistSuggestion? FindSuggestion(ChecklistSuggestionId id) => Suggestions.FirstOrDefault(s => s.Id == id);

    // A resolution names a suggestion the stream itself added -- an unknown id is a handler bug, not
    // input, so it throws like Reorder below.
    private static TaskTemplate Resolve(TaskTemplate template, ChecklistSuggestionId id, Func<ChecklistSuggestion, ChecklistSuggestion> resolve)
    {
        var index = template.Suggestions.FindIndex(s => s.Id == id);

        if (index < 0)
        {
            throw new InvalidOperationException($"A suggestion resolution referenced unknown suggestion {id}.");
        }

        return template with { Suggestions = template.Suggestions.SetItem(index, resolve(template.Suggestions[index])) };
    }

    // Every id in `after` must already exist in `subtasks` -- a fold-invariant violation here is a
    // real bug (the handler emitting SubtasksReordered is responsible for only ever appending a
    // valid permutation of the template's current subtask ids), not user input to validate
    // against, so this throws rather than degrading silently. See ReorderSubtasksHandler for the
    // handler-side permutation check that keeps this invariant true.
    private static ImmutableList<Subtask> Reorder(ImmutableList<Subtask> subtasks, ImmutableList<SubtaskId> after)
    {
        var byId = subtasks.ToDictionary(s => s.Id);

        return [.. after.Select(id => byId.TryGetValue(id, out var subtask)
            ? subtask
            : throw new InvalidOperationException($"SubtasksReordered referenced unknown subtask {id}."))];
    }
}
