using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// A child's medicine schedules, dose logs and group sharing are the child's alone: erasing the child
// deletes them. See gdpr-data-protection.md.
public sealed class MedicinesPersonalDataEraser(IMedicinesStore store, IMedicineEventStore medicines, IMedicineSharingEventStore sharing)
    : IPersonalDataEraser
{
    public Type Store => typeof(IMedicinesStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        foreach (var medicineId in await medicines.ListIdsForChildAsync(child.UserId, cancellationToken))
        {
            await store.DeleteStreamAsync<MedicineScheduleSnapshot>(
                medicineId.Value, cancellationToken, s => s.Delete<MedicineIndexDocument>(medicineId.Value));
        }

        if (await sharing.FindIdForChildAsync(child.UserId, cancellationToken) is { } sharingId)
        {
            await store.DeleteStreamAsync<MedicineSharingSnapshot>(
                sharingId.Value, cancellationToken, s => s.Delete<MedicineSharingIndexDocument>(sharingId.Value));
        }
    }
}
