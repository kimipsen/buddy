using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class UpdateWorkLocationHandler
{
    public static async Task<Result<WorkLocationSummary>> Handle(
        UpdateWorkLocation command,
        IValidator<UpdateWorkLocation> validator,
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

        // Archived locations can't be edited -- they only exist so old references keep resolving.
        if (loaded.Schedule.FindActiveLocation(command.LocationId) is not { } location)
        {
            return new Result<WorkLocationSummary>.NotFound();
        }

        var after = new WorkLocationDetails(command.Name.Trim(), new Icon(command.Icon), new Color(command.Color));

        if (after == location.Details)
        {
            return new Result<WorkLocationSummary>.Success(WorkLocationSummary.From(location));
        }

        if (WorkLocationRules.NameIsTaken(loaded.Schedule, after.Name, excluding: location.Id))
        {
            return new Result<WorkLocationSummary>.Validation(ValidationProblem.Of("A work location with this name already exists."));
        }

        var changed = new WorkLocationDetailsChanged(loaded.Schedule.Id, location.Id, location.Details, after, now);
        var schedule = await WorkLocationScheduleWriter.SaveAsync(store, loaded, [changed], now, cancellationToken);

        return new Result<WorkLocationSummary>.Success(WorkLocationSummary.From(schedule.FindLocation(location.Id)!));
    }
}
