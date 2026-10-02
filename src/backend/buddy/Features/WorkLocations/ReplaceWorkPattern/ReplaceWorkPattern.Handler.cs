using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class ReplaceWorkPatternHandler
{
    public static async Task<Result<WorkPatternResponse>> Handle(
        ReplaceWorkPattern command,
        IValidator<ReplaceWorkPattern> validator,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<WorkPatternResponse>.Validation(problem);
        }

        var userId = command.UserId;

        var access = await WorkLocationAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<WorkPatternResponse>();
        }

        var now = DateTimeOffset.UtcNow;
        var loaded = await WorkLocationScheduleWriter.LoadAsync(store, userId, now, cancellationToken);
        var schedule = loaded.Schedule;

        if (command.Pattern.Days.Any(day => schedule.FindActiveLocation(day.LocationId) is null))
        {
            return new Result<WorkPatternResponse>.Validation(ValidationProblem.Of("Every locationId must be one of your active work locations."));
        }

        var after = command.Pattern.Normalized();

        if (!after.IsSameAs(schedule.Pattern))
        {
            schedule = await WorkLocationScheduleWriter.SaveAsync(
                store, loaded, [new WorkPatternReplaced(schedule.Id, schedule.Pattern, after, now)], now, cancellationToken);
        }

        return new Result<WorkPatternResponse>.Success(WorkPatternResponse.From(schedule.Pattern));
    }
}
