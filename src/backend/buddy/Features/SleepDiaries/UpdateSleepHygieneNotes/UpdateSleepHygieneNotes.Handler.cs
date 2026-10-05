using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public static class UpdateSleepHygieneNotesHandler
{
    public static async Task<Result<Unit>> Handle(
        UpdateSleepHygieneNotes command,
        IValidator<UpdateSleepHygieneNotes> validator,
        ISleepDiaryEventStore diaries,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Unit>.Validation(problem);
        }

        var access = await SleepDiaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = SleepDiaryId.ForChild(command.ChildId);
        var events = await diaries.ReadAsync(id, cancellationToken);
        var before = events.Count == 0 ? "" : SleepDiary.Replay(events).SleepHygieneNotes;

        if (before == command.Notes)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var now = DateTimeOffset.UtcNow;
        var updated = new SleepHygieneNotesUpdated(id, before, command.Notes, command.UserId, now);

        if (events.Count == 0)
        {
            // Notes written before any night is logged still start the diary lazily.
            await diaries.CreateAsync(id, [new SleepDiaryStarted(id, command.ChildId, now), updated], cancellationToken);
        }
        else
        {
            await diaries.AppendAsync(id, [updated], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
