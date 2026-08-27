using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class ListTodaysDosesHandler
{
    // Keeps a single request's expansion work bounded regardless of how many schedules a child
    // has. Smaller than ListOccurrencesHandler's calendar-wide max -- this view is meant for
    // "today" or a short window, not a full-year export.
    public const int MaxRangeDays = 31;

    public static async Task<Result<IReadOnlyCollection<MedicineDoseOccurrence>>> Handle(
        ListTodaysDoses query,
        IValidator<ListTodaysDoses> validator,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation(problem);
        }

        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.NotFound();
        }

        var access = await MedicineAuthorization.CheckMark(query.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<MedicineDoseOccurrence>>();
        }

        var occurrences = await MedicineDoseExpansion.ExpandAsync(query.ChildId, query.From, query.To, medicines, cancellationToken);

        return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Success(occurrences);
    }
}
