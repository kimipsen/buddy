namespace buddy.Features.Pickups;

// The read/wire shape for one assigned slot -- adds Date/Slot to PickupAssignment's fields and
// unwraps its UserId? fields to raw Guid?, the same flattening MedicineScheduleResponse applies
// to its domain type. Only assigned slots are ever represented -- there is no "unplanned" entry;
// a date/slot absent from a ListPickupSchedule response is unplanned, the same sparse convention
// MealPlanExpansion uses.
public sealed record PickupOccurrence(
    DateOnly Date,
    PickupSlot Slot,
    PickupAssigneeKind Kind,
    Guid? GuardianId,
    Guid? SiblingChildId,
    string? PlaydateHostName,
    string? PlaydateLocation,
    string? PlaydateContactInfo,
    TimeOnly? Time,
    string? Notes,
    Guid AssignedBy)
{
    public static PickupOccurrence FromAssignment(DateOnly date, PickupSlot slot, PickupAssignment assignment) => new(
        date,
        slot,
        assignment.Kind,
        assignment.GuardianId?.Value,
        assignment.SiblingChildId?.Value,
        assignment.PlaydateHostName,
        assignment.PlaydateLocation,
        assignment.PlaydateContactInfo,
        assignment.Time,
        assignment.Notes,
        assignment.AssignedBy.Value);
}
