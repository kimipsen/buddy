using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// A child's night schedule -- with its sleepover hosts' names, addresses and contact details -- is
// the child's alone: erasing the child deletes it. Erasing a guardian leaves it in place, as for
// pickups; nights naming them stop resolving a name. See gdpr-data-protection.md and
// docs/backend/analysis/night-duty.md, "GDPR".
public sealed class NightDutyPersonalDataEraser(INightDutyStore store) : IPersonalDataEraser
{
    public Type Store => typeof(INightDutyStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<NightScheduleSnapshot>(NightScheduleId.ForChild(child.UserId).Value, cancellationToken);
}
