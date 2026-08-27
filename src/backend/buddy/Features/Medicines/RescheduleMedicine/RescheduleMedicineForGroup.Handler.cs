using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class RescheduleMedicineForGroupHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        RescheduleMedicineForGroup command,
        IValidator<RescheduleMedicineForGroup> validator,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MedicineSchedule>.Validation(problem);
        }

        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, MedicineSchedule>();
        }

        var result = await RescheduleMedicineHandler.RescheduleForChildAsync(
            command.ChildId, command.MedicineId, command.UserId!, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);

        return result is null ? new Result<MedicineSchedule>.NotFound() : new Result<MedicineSchedule>.Success(result);
    }
}
