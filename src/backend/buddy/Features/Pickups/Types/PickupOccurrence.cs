namespace buddy.Features.Pickups;

// The read/wire shape for one assigned slot -- PickupAssignment plus its Date/Slot, with the
// assignee as PickupAssigneeDto. Only assigned slots are ever represented -- there is no
// "unplanned" entry; a date/slot absent from a ListPickupSchedule response is unplanned, the same
// sparse convention MealPlanExpansion uses.
public sealed record PickupOccurrence(
    DateOnly Date,
    PickupSlot Slot,
    PickupAssigneeDto Assignee,
    TimeOnly? Time,
    string Notes,
    Guid AssignedBy)
{
    public static PickupOccurrence FromAssignment(DateOnly date, PickupSlot slot, PickupAssignment assignment) => new(
        date,
        slot,
        PickupAssigneeDto.FromDomain(assignment.Assignee),
        assignment.Time,
        assignment.Notes,
        assignment.AssignedBy.Value);
}
