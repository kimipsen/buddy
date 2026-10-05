using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public static class LogSleepEntryHandler
{
    public static async Task<Result<SleepEntryResponse>> Handle(
        LogSleepEntry command,
        IValidator<LogSleepEntry> validator,
        ISleepDiaryEventStore diaries,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<SleepEntryResponse>.Validation(problem);
        }

        var access = await SleepDiaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<SleepEntryResponse>();
        }

        var id = SleepDiaryId.ForChild(command.ChildId);
        var after = command.ToEntry();
        var now = DateTimeOffset.UtcNow;

        var events = await diaries.ReadAsync(id, cancellationToken);

        if (events.Count == 0)
        {
            await diaries.CreateAsync(
                id,
                [
                    new SleepDiaryStarted(id, command.ChildId, now),
                    new SleepEntryLogged(id, command.Date, null, after, now)
                ],
                cancellationToken);

            return new Result<SleepEntryResponse>.Success(SleepEntryResponse.From(command.Date, after));
        }

        var diary = SleepDiary.Replay(events);
        var before = diary.Entries.GetValueOrDefault(command.Date);

        // Full overwrite; an identical re-save (even by the other guardian) appends nothing, which
        // keeps the PUT idempotent. The response then shows the entry as it was first logged.
        if (before is not null && before.HasSameContentAs(after))
        {
            return new Result<SleepEntryResponse>.Success(SleepEntryResponse.From(command.Date, before));
        }

        await diaries.AppendAsync(id, [new SleepEntryLogged(id, command.Date, before, after, now)], cancellationToken);

        return new Result<SleepEntryResponse>.Success(SleepEntryResponse.From(command.Date, after));
    }
}
