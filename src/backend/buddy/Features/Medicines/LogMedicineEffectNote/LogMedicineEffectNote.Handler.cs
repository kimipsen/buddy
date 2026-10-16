using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class LogMedicineEffectNoteHandler
{
    public static async Task<Result<MedicineEffectNoteResponse>> Handle(
        LogMedicineEffectNote command,
        IValidator<LogMedicineEffectNote> validator,
        IMedicineEffectLogEventStore effectLogs,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MedicineEffectNoteResponse>.Validation(problem);
        }

        var access = await MedicineAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineEffectNoteResponse>();
        }

        if (await MedicineEffectNotes.ChildTodayAsync(command.ChildId, users, time, cancellationToken) is not { } today)
        {
            return new Result<MedicineEffectNoteResponse>.NotFound();
        }

        if (command.Date > today)
        {
            return new Result<MedicineEffectNoteResponse>.Validation(ValidationProblem.Of("A note can't be for a day after today on the child's clock."));
        }

        var id = MedicineEffectLogId.ForChild(command.ChildId);
        var now = time.GetUtcNow();
        var schedules = await MedicineEffectNotes.LoadSchedulesAsync(command.ChildId, medicines, cancellationToken);

        var events = await effectLogs.ReadAsync(id, cancellationToken);

        if (events.Count == 0)
        {
            var first = command.ToNote(MedicineEffectNotes.ResolveRegimen(schedules.Values, command.Date));

            await effectLogs.CreateAsync(
                id,
                [
                    new MedicineEffectLogStarted(id, command.ChildId, now),
                    new MedicineEffectNoteLogged(id, command.Date, null, first, now)
                ],
                cancellationToken);

            return new Result<MedicineEffectNoteResponse>.Success(MedicineEffectNoteResponse.From(first, schedules));
        }

        var before = MedicineEffectLog.Replay(events).Notes.GetValueOrDefault(command.Date);

        // The regimen is resolved when the day is first logged and kept on later edits, so fixing a
        // typo after a dosage change doesn't swap in the new dosage (Question 4).
        var after = command.ToNote(before?.Regimen ?? MedicineEffectNotes.ResolveRegimen(schedules.Values, command.Date));

        if (before is not null && before.HasSameContentAs(after))
        {
            return new Result<MedicineEffectNoteResponse>.Success(MedicineEffectNoteResponse.From(before, schedules));
        }

        await effectLogs.AppendAsync(id, [new MedicineEffectNoteLogged(id, command.Date, before, after, now)], cancellationToken);

        return new Result<MedicineEffectNoteResponse>.Success(MedicineEffectNoteResponse.From(after, schedules));
    }
}
