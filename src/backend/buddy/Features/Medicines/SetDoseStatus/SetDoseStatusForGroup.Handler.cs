using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class SetDoseStatusForGroupHandler
{
    public static async Task<Result<MedicineDoseOccurrence>> Handle(
        SetDoseStatusForGroup command,
        IValidator<SetDoseStatusForGroup> validator,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MedicineDoseOccurrence>.Validation(problem);
        }

        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, MedicineDoseOccurrence>();
        }

        return await SetDoseStatusHandler.SetForChildAsync(command.ChildId, command.MedicineId, command.Date, command.Time, command.Status, command.UserId, medicines, time, cancellationToken);
    }
}
