using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// A child's pickup schedule -- with its playdate hosts' names, addresses and contact details -- is
// the child's alone: erasing the child deletes it. A guardian's PickupFeed (their iCal tokens) is
// theirs alone: erasing the guardian deletes it, so every subscription link stops working. See
// gdpr-data-protection.md.
public sealed class PickupsPersonalDataEraser(IPickupsStore store, IPickupScheduleEventStore schedules) : IPersonalDataEraser
{
    public Type Store => typeof(IPickupsStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<PickupFeedSnapshot>(PickupFeedId.ForGuardian(guardian.UserId).Value, cancellationToken);

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        if (await schedules.FindIdForChildAsync(child.UserId, cancellationToken) is { } scheduleId)
        {
            await store.DeleteStreamAsync<PickupScheduleSnapshot>(
                scheduleId.Value, cancellationToken, s => s.Delete<PickupScheduleIndexDocument>(scheduleId.Value));
        }
    }
}
