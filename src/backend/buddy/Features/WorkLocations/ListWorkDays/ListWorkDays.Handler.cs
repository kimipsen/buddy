using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public static class ListWorkDaysHandler
{
    public static async Task<Result<IReadOnlyCollection<WorkDay>>> Handle(
        ListWorkDays query,
        IValidator<ListWorkDays> validator,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<WorkDay>>.Validation(problem);
        }

        var userId = query.UserId;

        var access = await WorkLocationAuthorization.CheckView(query.GuardianId, userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<WorkDay>>();
        }

        var days = await WorkLocationExpansion.ExpandAsync(query.GuardianId, query.From, query.To, store, cancellationToken);

        return new Result<IReadOnlyCollection<WorkDay>>.Success(days);
    }
}
