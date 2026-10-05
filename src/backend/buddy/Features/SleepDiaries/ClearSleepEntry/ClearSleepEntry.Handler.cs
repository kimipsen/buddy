using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.SleepDiaries;

public static class ClearSleepEntryHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearSleepEntry command,
        ISleepDiaryEventStore diaries,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await SleepDiaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = SleepDiaryId.ForChild(command.ChildId);
        var diary = SleepDiary.Rehydrate(await diaries.ReadAsync(id, cancellationToken));

        // No diary yet, or nothing logged that day -- clearing is an idempotent no-op, the same
        // rule ClearPickup/ClearMealSlot use.
        if (diary?.Entries.GetValueOrDefault(command.Date) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await diaries.AppendAsync(id, [new SleepEntryCleared(id, command.Date, before, command.UserId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
