using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleForGroupHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        CreateMedicineScheduleForGroup command,
        IValidator<CreateMedicineScheduleForGroup> validator,
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

        var schedule = await CreateMedicineScheduleHandler.CreateForChildAsync(
            command.ChildId, command.UserId!, command.Name, command.Dosage, command.Icon, command.Color, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule);
    }
}
