using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// A child's feelings journal and its share links are the child's alone: erasing the child deletes
// them. A guardian who is erased loses the share links they created for children who stay with a
// co-guardian (a doctor shouldn't keep reading after they've gone); the check-ins and notes they
// logged stay in the child's record, with LoggedBy a pseudonym once the user is erased
// (docs/backend/analysis/feelings-check-in.md, GDPR and privacy).
public sealed class FeelingsPersonalDataEraser(
    IFeelingsStore store,
    IFeelingsShareTokenEventStore shareTokens,
    IGuardianLinkEventStore guardians) : IPersonalDataEraser
{
    public Type Store => typeof(IFeelingsStore);

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        var links = await guardians.ListForGuardianAsync(guardian.UserId, cancellationToken);
        var revokedLinks = await guardians.ListRevokedForGuardianAsync(guardian.UserId, cancellationToken);

        foreach (var childId in links.Concat(revokedLinks).Select(l => new UserId(l.ChildId)).Distinct())
        {
            foreach (var document in await shareTokens.ListForChildAsync(childId, cancellationToken))
            {
                var tokenId = new FeelingsShareTokenId(document.Id);

                if (!document.IsRevoked
                    && await shareTokens.FindSnapshotAsync(tokenId, cancellationToken) is { } token
                    && token.CreatedBy == guardian.UserId)
                {
                    await shareTokens.AppendAsync(tokenId, [new FeelingsShareTokenRevoked(tokenId, guardian.UserId, DateTimeOffset.UtcNow)], cancellationToken);
                }
            }
        }
    }

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        await store.DeleteStreamAsync<FeelingsJournalSnapshot>(FeelingsJournalId.ForChild(child.UserId).Value, cancellationToken);

        foreach (var document in await shareTokens.ListForChildAsync(child.UserId, cancellationToken))
        {
            await store.DeleteStreamAsync<FeelingsShareTokenSnapshot>(document.Id, cancellationToken, s => s.Delete(document));
        }
    }
}
