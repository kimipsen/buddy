using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record AssignPickup(
    UserId UserId,
    UserId ChildId,
    DateOnly Date,
    PickupSlot Slot,
    PickupAssignee Assignee,
    TimeOnly? Time,
    string Notes)
{
    public static AssignPickup FromClaims(
        ClaimsPrincipal principal,
        UserId childId,
        DateOnly date,
        PickupSlot slot,
        PickupAssignee assignee,
        TimeOnly? time,
        string notes) =>
        new(principal.GetRequiredUserId(), childId, date, slot, assignee, time, notes);
}
