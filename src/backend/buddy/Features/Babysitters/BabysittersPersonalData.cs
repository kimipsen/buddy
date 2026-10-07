using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.Babysitters;

// A guardian's babysitter list is theirs alone, keyed by their UserId: erasing them deletes it,
// and with it the babysitters' names and contact details. See gdpr-data-protection.md.
public sealed class BabysittersPersonalDataEraser(IBabysittersStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IBabysittersStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<BabysitterListSnapshot>(BabysitterListId.ForGuardian(guardian.UserId).Value, cancellationToken);

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) => Task.CompletedTask;
}
