using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Task templates belong to the family, anchored to one child in TaskTemplateIndexDocument: an erased
// child's templates pass to their heir (a sibling), or go with them when no sibling is left. See
// gdpr-data-protection.md.
public sealed class TaskLibraryPersonalDataEraser(ITaskLibraryStore store, ITaskTemplateEventStore templates) : IPersonalDataEraser
{
    public Type Store => typeof(ITaskLibraryStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        foreach (var templateId in await templates.ListIdsForChildAsync(child.UserId, cancellationToken))
        {
            if (heir is null)
            {
                await store.DeleteStreamAsync<TaskTemplateSnapshot>(
                    templateId.Value, cancellationToken, s => s.Delete<TaskTemplateIndexDocument>(templateId.Value));
                continue;
            }

            await using var session = store.LightweightSession();
            session.Store(new TaskTemplateIndexDocument(templateId.Value, heir.Value));
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}
