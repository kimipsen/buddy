using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.NightDuty;

// Resolved nights for a guardian (the editor's week, the print sheet) and for the child themself
// (the "Tonight" card).
public static class ListNightsHandler
{
    public static async Task<Result<IReadOnlyCollection<Night>>> Handle(
        ListNights query,
        IValidator<ListNights> validator,
        INightScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        IBabysitterListEventStore babysitters,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<Night>>.Validation(problem);
        }

        var access = await NightDutyAuthorization.CheckView(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<Night>>();
        }

        var nights = await NightExpansion.ExpandAsync(query.ChildId, query.From, query.To, store, babysitters, cancellationToken);

        return new Result<IReadOnlyCollection<Night>>.Success(nights);
    }
}
