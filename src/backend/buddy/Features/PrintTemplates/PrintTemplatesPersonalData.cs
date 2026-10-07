using buddy.Common.Erasure;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.PrintTemplates;

// Print templates a guardian owns personally (not group-owned ones) are deleted with them. Rows that
// name an erased child keep only its id. See gdpr-data-protection.md.
public sealed class PrintTemplatesPersonalDataEraser(IPrintTemplatesStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IPrintTemplatesStore);

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        IReadOnlyList<PrintTemplateIndexDocument> owned;

        await using (var session = store.QuerySession())
        {
            owned = await session.Query<PrintTemplateIndexDocument>()
                .Where(d => d.OwnerUserId == guardian.UserId.Value)
                .ToListAsync(cancellationToken);
        }

        foreach (var template in owned)
        {
            await store.DeleteStreamAsync<PrintTemplateSnapshot>(template.Id, cancellationToken, s => s.Delete(template));
        }
    }

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) => Task.CompletedTask;
}
