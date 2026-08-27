using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class ListTodaysDosesForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<MedicineDoseOccurrence>>> Handle(
        ListTodaysDosesForGroup query,
        IValidator<ListTodaysDosesForGroup> validator,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation(problem);
        }

        var resolved = await MedicineGroupAccess.ResolveAsync(query.GroupId, query.ChildId, query.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, IReadOnlyCollection<MedicineDoseOccurrence>>();
        }

        var occurrences = await MedicineDoseExpansion.ExpandAsync(query.ChildId, query.From, query.To, medicines, cancellationToken);

        return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Success(occurrences);
    }
}
