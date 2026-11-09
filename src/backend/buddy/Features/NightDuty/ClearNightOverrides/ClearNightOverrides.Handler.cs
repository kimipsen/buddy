using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.NightDuty;

public static class ClearNightOverridesHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearNightOverrides command,
        IValidator<ClearNightOverrides> validator,
        INightScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Unit>.Validation(problem);
        }

        var access = await NightDutyAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var now = time.GetUtcNow();
        var loaded = await NightScheduleWriter.LoadAsync(store, command.ChildId, cancellationToken);
        var schedule = loaded.Schedule;

        // Idempotent: only dates that actually hold an override emit an event, so clearing an empty
        // range (or a child with no stream at all) is a no-op Success.
        List<NightDutyEvent> events = [.. schedule.Overrides
            .Where(entry => entry.Key >= command.From && entry.Key <= command.To)
            .OrderBy(entry => entry.Key)
            .Select(entry => (NightDutyEvent)new NightOverrideCleared(schedule.Id, entry.Key, entry.Value, command.UserId, now))];

        await NightScheduleWriter.SaveAsync(store, loaded, events, now, cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
