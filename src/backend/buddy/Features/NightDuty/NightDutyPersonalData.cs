using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// A child's night schedule -- with its sleepover hosts' names, addresses and contact details -- is
// the child's alone: erasing the child deletes it. Erasing a guardian leaves it in place, as for
// pickups; nights naming them stop resolving a name. A guardian's NightFeed (their iCal tokens) is
// theirs alone: erasing the guardian deletes it, so every subscription link stops working. See
// gdpr-data-protection.md, docs/backend/analysis/night-duty.md, "GDPR", and night-duty-ical-feed.md.
public sealed class NightDutyPersonalDataEraser(INightDutyStore store) : IPersonalDataEraser
{
    public Type Store => typeof(INightDutyStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<NightFeedSnapshot>(NightFeedId.ForGuardian(guardian.UserId).Value, cancellationToken);

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<NightScheduleSnapshot>(NightScheduleId.ForChild(child.UserId).Value, cancellationToken);
}
