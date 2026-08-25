using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Pickups;

public static class ListPickupScheduleHandler
{
    // Keeps a single request's expansion work bounded regardless of schedule size -- same
    // rationale and value as ListTodaysDosesHandler.MaxRangeDays/ListMealPlanHandler.MaxRangeDays.
    public const int MaxRangeDays = 31;

    public static async Task<Result<IReadOnlyCollection<PickupOccurrence>>> Handle(
        ListPickupSchedule query,
        IPickupScheduleEventStore pickups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new Result<IReadOnlyCollection<PickupOccurrence>>.Validation("'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxRangeDays)
        {
            return new Result<IReadOnlyCollection<PickupOccurrence>>.Validation($"The requested range cannot exceed {MaxRangeDays} days.");
        }

        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<PickupOccurrence>>.NotFound();
        }

        var access = await PickupAuthorization.CheckView(query.ChildId, userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<PickupOccurrence>>();
        }

        var occurrences = await PickupScheduleExpansion.ExpandAsync(query.ChildId, query.From, query.To, pickups, cancellationToken);

        return new Result<IReadOnlyCollection<PickupOccurrence>>.Success(occurrences);
    }
}
