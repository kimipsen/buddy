using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Pickups;

public static class ListPickupScheduleHandler
{
    // Keeps a single request's expansion work bounded regardless of schedule size -- same
    // rationale and value as ListTodaysDosesHandler.MaxRangeDays/ListMealPlanHandler.MaxRangeDays.
    public const int MaxRangeDays = 31;

    public static async Task<Result<IReadOnlyCollection<PickupOccurrence>>> Handle(
        ListPickupSchedule query,
        IValidator<ListPickupSchedule> validator,
        IPickupScheduleEventStore pickups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<PickupOccurrence>>.Validation(problem);
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
