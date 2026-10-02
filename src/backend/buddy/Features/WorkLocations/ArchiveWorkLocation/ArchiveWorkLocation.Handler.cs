using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.WorkLocations;

public static class ArchiveWorkLocationHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveWorkLocation command,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await WorkLocationAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var now = DateTimeOffset.UtcNow;
        var loaded = await WorkLocationScheduleWriter.LoadAsync(store, userId, now, cancellationToken);

        if (loaded.Schedule.FindLocation(command.LocationId) is not { } location)
        {
            return new Result<Unit>.NotFound();
        }

        if (location.IsArchived)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var schedule = loaded.Schedule;
        var events = new List<WorkLocationEvent>();

        // No pattern may point at an archived location, so the pattern loses that location's days in
        // the same append. Overrides that point at it are left alone and keep resolving (see
        // docs/backend/analysis/work-locations.md, Question 3).
        if (schedule.Pattern.Uses(location.Id))
        {
            events.Add(new WorkPatternReplaced(schedule.Id, schedule.Pattern, schedule.Pattern.Without(location.Id), now));
        }

        events.Add(new WorkLocationArchived(schedule.Id, location.Id, now));

        await WorkLocationScheduleWriter.SaveAsync(store, loaded, events, now, cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
