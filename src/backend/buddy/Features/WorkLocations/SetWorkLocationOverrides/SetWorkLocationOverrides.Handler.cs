using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class SetWorkLocationOverridesHandler
{
    public static async Task<Result<IReadOnlyCollection<WorkDay>>> Handle(
        SetWorkLocationOverrides command,
        IValidator<SetWorkLocationOverrides> validator,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<WorkDay>>.Validation(problem);
        }

        var userId = command.UserId;

        var access = await WorkLocationAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<WorkDay>>();
        }

        var now = DateTimeOffset.UtcNow;
        var loaded = await WorkLocationScheduleWriter.LoadAsync(store, userId, now, cancellationToken);
        var schedule = loaded.Schedule;

        if (command.LocationId is { } locationId && schedule.FindActiveLocation(locationId) is null)
        {
            return new Result<IReadOnlyCollection<WorkDay>>.Validation(ValidationProblem.Of("locationId must be one of your active work locations."));
        }

        var after = new WorkDayOverride(command.LocationId);
        var events = new List<WorkLocationEvent>();

        // One event per date that actually changes; dates already holding this override emit nothing.
        for (var date = command.From; date <= command.To; date = date.AddDays(1))
        {
            var before = schedule.Overrides.GetValueOrDefault(date);

            if (before != after)
            {
                events.Add(new WorkLocationOverridden(schedule.Id, date, before, after, now));
            }
        }

        schedule = await WorkLocationScheduleWriter.SaveAsync(store, loaded, events, now, cancellationToken);

        return new Result<IReadOnlyCollection<WorkDay>>.Success(WorkLocationExpansion.Expand(schedule, command.From, command.To));
    }
}
