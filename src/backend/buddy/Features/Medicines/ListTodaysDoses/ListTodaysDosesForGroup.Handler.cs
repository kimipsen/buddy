using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class ListTodaysDosesForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<MedicineDoseOccurrence>>> Handle(
        ListTodaysDosesForGroup query,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation("'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > ListTodaysDosesHandler.MaxRangeDays)
        {
            return new Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation($"The requested range cannot exceed {ListTodaysDosesHandler.MaxRangeDays} days.");
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
