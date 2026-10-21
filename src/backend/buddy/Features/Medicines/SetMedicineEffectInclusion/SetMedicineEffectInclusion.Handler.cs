using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

// Guardian-only, like the effect notes it affects; no group variant
// (docs/backend/analysis/medicine-effect-inclusion.md, Question 4).
public static class SetMedicineEffectInclusionHandler
{
    public static async Task<Result<Unit>> Handle(
        SetMedicineEffectInclusion command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await MedicineAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var events = await medicines.ReadAsync(command.MedicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        // Unlike UpdateMedicineDetails, a stopped schedule is allowed: it still shows up in the
        // regimens of older effect notes, which are the periods the guardian wants merged.
        if (schedule is null || schedule.ChildId != command.ChildId)
        {
            return new Result<Unit>.NotFound();
        }

        var excluded = !command.Included;

        if (schedule.IsExcludedFromEffects != excluded)
        {
            await medicines.AppendAsync(
                command.MedicineId,
                [new MedicineEffectInclusionChanged(command.MedicineId, schedule.IsExcludedFromEffects, excluded, command.UserId, time.GetUtcNow())],
                cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
