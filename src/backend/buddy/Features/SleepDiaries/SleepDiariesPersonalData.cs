using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// A child's sleep diary and its share links are the child's alone: erasing the child deletes them.
// A guardian who is erased loses the share links they created for children who stay with a
// co-guardian: whoever they handed a link to (a doctor) shouldn't keep reading after they've gone.
// See gdpr-data-protection.md.
public sealed class SleepDiariesPersonalDataEraser(
    ISleepDiariesStore store,
    ISleepDiaryShareTokenEventStore shareTokens,
    IGuardianLinkEventStore guardians) : IPersonalDataEraser
{
    public Type Store => typeof(ISleepDiariesStore);

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        var links = await guardians.ListForGuardianAsync(guardian.UserId, cancellationToken);
        var revokedLinks = await guardians.ListRevokedForGuardianAsync(guardian.UserId, cancellationToken);

        foreach (var childId in links.Concat(revokedLinks).Select(l => new UserId(l.ChildId)).Distinct())
        {
            foreach (var document in await shareTokens.ListForChildAsync(childId, cancellationToken))
            {
                var tokenId = new SleepDiaryShareTokenId(document.Id);

                if (!document.IsRevoked
                    && await shareTokens.FindSnapshotAsync(tokenId, cancellationToken) is { } token
                    && token.CreatedBy == guardian.UserId)
                {
                    await shareTokens.AppendAsync(tokenId, [new SleepDiaryShareTokenRevoked(tokenId, guardian.UserId, DateTimeOffset.UtcNow)], cancellationToken);
                }
            }
        }
    }

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        await store.DeleteStreamAsync<SleepDiarySnapshot>(SleepDiaryId.ForChild(child.UserId).Value, cancellationToken);

        foreach (var document in await shareTokens.ListForChildAsync(child.UserId, cancellationToken))
        {
            await store.DeleteStreamAsync<SleepDiaryShareTokenSnapshot>(document.Id, cancellationToken, s => s.Delete(document));
        }
    }
}
