using buddy.Common.Erasure;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.TaskLibrary;

// Task templates belong to the family, anchored to one child in TaskTemplateIndexDocument: an erased
// child's templates pass to their heir (a sibling), or go with them when no sibling is left. See
// gdpr-data-protection.md. A list that passes to an heir keeps the erased child's suggestions out of
// the heir's hands: pending ones are withdrawn and every suggestion title is masked; items a
// guardian approved stay, they are the family's list (docs/backend/analysis/packing-checklists.md).
public sealed class TaskLibraryPersonalDataEraser(ITaskLibraryStore store, ITaskTemplateEventStore templates, TimeProvider time) : IPersonalDataEraser
{
    public Type Store => typeof(ITaskLibraryStore);

    public static void ConfigureMasking(StoreOptions options) =>
        options.Events.AddMaskingRuleForProtectedInformation<ChecklistItemSuggested>(e => e with { Title = Erased.Text });

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

            await EraseSuggestionsAsync(templateId, child.UserId, cancellationToken);

            await using var session = store.LightweightSession();
            session.Store(new TaskTemplateIndexDocument(templateId.Value, heir.Value));
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    // Withdraws the pending suggestions first (so they can't be approved onto the heir's list), then
    // masks every suggestion title in the stream and rebuilds the snapshot. A list with no
    // suggestions (every routine) is left untouched.
    private async Task EraseSuggestionsAsync(TaskTemplateId templateId, UserId childId, CancellationToken cancellationToken)
    {
        if (await templates.FindSnapshotAsync(templateId, cancellationToken) is not { Suggestions.Count: > 0 } template)
        {
            return;
        }

        var now = time.GetUtcNow();
        TaskTemplateEvent[] withdrawn =
        [
            .. template.Suggestions
                .Where(s => s.IsPending)
                .Select(s => (TaskTemplateEvent)new ChecklistItemSuggestionWithdrawn(templateId, s.Id, childId, now)),
        ];

        await templates.AppendAsync(templateId, withdrawn, cancellationToken);
        await store.MaskStreamAsync<TaskTemplateSnapshot>(templateId.Value, cancellationToken);
    }
}
