using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        CreateMedicineSchedule command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires a name.");
        }

        if (command.Times.Count == 0)
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires at least one dose time.");
        }

        if (command.EndDate is { } end && end < command.StartDate)
        {
            return new Result<MedicineSchedule>.Validation("The end date cannot be before the start date.");
        }

        if (command.UserId is not { } userId)
        {
            return new Result<MedicineSchedule>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineSchedule>();
        }

        var schedule = await CreateForChildAsync(
            command.ChildId, userId, command.Name, command.Dosage, command.Icon, command.Color, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule);
    }

    // Shared with CreateMedicineScheduleForGroupHandler, which resolves its own acting guardian
    // through a group's MedicinePermissionPolicy instead of MedicineAuthorization -- everything
    // past authorization is identical (see CreateMealHandler.CreateForChildAsync for the same
    // pattern in Mealplans).
    internal static async Task<MedicineSchedule> CreateForChildAsync(
        UserId childId,
        UserId createdBy,
        string name,
        string dosage,
        Icon icon,
        Color color,
        IReadOnlyList<TimeOnly> times,
        DateOnly startDate,
        DateOnly? endDate,
        IMedicineEventStore medicines,
        CancellationToken cancellationToken)
    {
        var medicineId = MedicineId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new MedicineScheduleCreated(medicineId, childId, createdBy, name, dosage, icon, color, times, startDate, endDate, now);

        var events = await medicines.CreateAsync(medicineId, [created], cancellationToken);

        return MedicineSchedule.Rehydrate(events)!;
    }
}
