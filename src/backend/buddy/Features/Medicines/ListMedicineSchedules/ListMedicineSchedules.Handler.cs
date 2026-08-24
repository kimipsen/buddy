using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public static class ListMedicineSchedulesHandler
{
    public static async Task<Result<IReadOnlyCollection<MedicineSchedule>>> Handle(
        ListMedicineSchedules query,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<MedicineSchedule>>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<MedicineSchedule>>();
        }

        var loaded = await ListForChildAsync(query.ChildId, medicines, cancellationToken);

        return new Result<IReadOnlyCollection<MedicineSchedule>>.Success(loaded);
    }

    // Shared with ListMedicineSchedulesForGroupHandler -- everything past authorization is
    // identical.
    internal static async Task<IReadOnlyCollection<MedicineSchedule>> ListForChildAsync(UserId childId, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var medicineIds = await medicines.ListIdsForChildAsync(childId, cancellationToken);
        var loaded = new List<MedicineSchedule>(medicineIds.Count);

        foreach (var medicineId in medicineIds)
        {
            var events = await medicines.ReadAsync(medicineId, cancellationToken);

            // Deliberately includes stopped schedules -- a guardian's history of a child's
            // courses, not just the active ones (see docs/backend/analysis/medicine-schedules.md).
            if (MedicineSchedule.Rehydrate(events) is { } schedule)
            {
                loaded.Add(schedule);
            }
        }

        loaded.Sort((a, b) => a.StartDate.CompareTo(b.StartDate));

        return loaded;
    }
}
