using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class ClearMedicineEffectNoteHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearMedicineEffectNote command,
        IMedicineEffectLogEventStore effectLogs,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await MedicineAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = MedicineEffectLogId.ForChild(command.ChildId);
        var log = MedicineEffectLog.Rehydrate(await effectLogs.ReadAsync(id, cancellationToken));

        // No log yet, or no note that day -- an idempotent no-op, as ClearSleepEntry.
        if (log?.Notes.GetValueOrDefault(command.Date) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await effectLogs.AppendAsync(id, [new MedicineEffectNoteCleared(id, command.Date, before, command.UserId, time.GetUtcNow())], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
