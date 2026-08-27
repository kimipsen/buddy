using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public interface ITaskTemplateEventStore
{
    Task<IReadOnlyCollection<TaskTemplateEvent>> ReadAsync(TaskTemplateId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TaskTemplateEvent>> CreateAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TaskTemplateId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken);

    // Reverse lookup from a bare TaskTemplateId to its owning child -- needed because, unlike
    // Mealplans' Update/Archive commands, the TaskLibrary write use-cases past creation (AddSubtask,
    // UpdateSubtask, RemoveSubtask, ReorderSubtasks, UpdateTaskTemplate, ArchiveTaskTemplate) carry
    // only a TemplateId, not a ChildId (see docs deviation note in TaskTemplateAccess). Backed by
    // the same TaskTemplateIndexDocument ListIdsForChildAsync queries, just filtered by Id instead
    // of ChildId.
    Task<UserId?> FindChildIdForTemplateAsync(TaskTemplateId id, CancellationToken cancellationToken);
}
