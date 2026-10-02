using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class ClearWorkLocationOverridesHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearWorkLocationOverrides command,
        IValidator<ClearWorkLocationOverrides> validator,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Unit>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await WorkLocationAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var now = DateTimeOffset.UtcNow;
        var loaded = await WorkLocationScheduleWriter.LoadAsync(store, userId, now, cancellationToken);
        var schedule = loaded.Schedule;

        // Idempotent: only dates that actually hold an override emit an event, so clearing an empty
        // range (or a guardian with no stream at all) is a no-op Success.
        var events = schedule.Overrides
            .Where(entry => entry.Key >= command.From && entry.Key <= command.To)
            .OrderBy(entry => entry.Key)
            .Select(entry => (WorkLocationEvent)new WorkLocationOverrideCleared(schedule.Id, entry.Key, entry.Value, now))
            .ToList();

        await WorkLocationScheduleWriter.SaveAsync(store, loaded, events, now, cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
