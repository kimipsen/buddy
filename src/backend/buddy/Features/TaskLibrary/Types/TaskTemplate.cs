using System.Collections.Immutable;

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
    UserId LastModifiedBy)
{
    public TimeSpan TotalDuration => Subtasks.Aggregate(TimeSpan.Zero, (sum, subtask) => sum + subtask.Duration);

    public static TaskTemplate? Rehydrate(IEnumerable<TaskTemplateEvent> events)
    {
        TaskTemplate? template = null;

        foreach (var @event in events)
        {
            template = @event switch
            {
                TaskTemplateCreated created => new TaskTemplate(
                    created.Id,
                    created.CreatedBy,
                    created.Name,
                    created.Icon,
                    created.Color,
                    ImmutableList<Subtask>.Empty,
                    IsArchived: false,
                    created.CreatedBy),
                TaskTemplateDetailsUpdated updated => template! with
                {
                    Name = updated.After.Name,
                    Icon = updated.After.Icon,
                    Color = updated.After.Color,
                    LastModifiedBy = updated.ModifiedBy
                },
                SubtaskAdded added => template! with
                {
                    Subtasks = template!.Subtasks.Insert(Math.Clamp(added.Position, 0, template.Subtasks.Count), added.Subtask),
                    LastModifiedBy = added.ModifiedBy
                },
                SubtaskUpdated updated => template! with
                {
                    Subtasks = template!.Subtasks.SetItem(
                        template.Subtasks.FindIndex(s => s.Id == updated.SubtaskId),
                        updated.After),
                    LastModifiedBy = updated.ModifiedBy
                },
                SubtaskRemoved removed => template! with
                {
                    Subtasks = template!.Subtasks.RemoveAll(s => s.Id == removed.SubtaskId),
                    LastModifiedBy = removed.ModifiedBy
                },
                SubtasksReordered reordered => template! with
                {
                    Subtasks = Reorder(template!.Subtasks, reordered.After),
                    LastModifiedBy = reordered.ModifiedBy
                },
                TaskTemplateArchived archived => template! with { IsArchived = true, LastModifiedBy = archived.ModifiedBy },
                _ => template
            };
        }

        return template;
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
