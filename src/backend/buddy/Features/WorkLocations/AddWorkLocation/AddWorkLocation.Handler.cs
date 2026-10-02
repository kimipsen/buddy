using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class AddWorkLocationHandler
{
    public static async Task<Result<WorkLocationSummary>> Handle(
        AddWorkLocation command,
        IValidator<AddWorkLocation> validator,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<WorkLocationSummary>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<WorkLocationSummary>.NotFound();
        }

        var access = await WorkLocationAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<WorkLocationSummary>();
        }

        var now = DateTimeOffset.UtcNow;
        var loaded = await WorkLocationScheduleWriter.LoadAsync(store, userId, now, cancellationToken);
        var name = command.Name.Trim();

        if (loaded.Schedule.ActiveLocations.Count() >= WorkLocationRules.MaxActiveLocations)
        {
            return new Result<WorkLocationSummary>.Validation(ValidationProblem.Of($"A guardian can have at most {WorkLocationRules.MaxActiveLocations} work locations."));
        }

        if (WorkLocationRules.NameIsTaken(loaded.Schedule, name))
        {
            return new Result<WorkLocationSummary>.Validation(ValidationProblem.Of("A work location with this name already exists."));
        }

        var locationId = WorkLocationId.New();
        var added = new WorkLocationAdded(loaded.Schedule.Id, locationId, name, new Icon(command.Icon), new Color(command.Color), now);
        var schedule = await WorkLocationScheduleWriter.SaveAsync(store, loaded, [added], now, cancellationToken);

        return new Result<WorkLocationSummary>.Success(WorkLocationSummary.From(schedule.FindLocation(locationId)!));
    }
}
