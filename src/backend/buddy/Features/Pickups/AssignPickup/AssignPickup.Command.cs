using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

// GuardianId/SiblingChildId/Playdate* are only meaningful for their matching Kind -- see
// AssignPickupHandler.BuildAssignee, which is where that's validated. Flattened onto the command
// the same way CreateMedicineSchedule flattens Icon/Color rather than accepting a pre-built value
// object, since PickupAssignee never crosses the HTTP boundary directly (see PickupAssigneeKind).
public sealed record AssignPickup(
    UserId? UserId,
    UserId ChildId,
    DateOnly Date,
    PickupSlot Slot,
    PickupAssigneeKind Kind,
    UserId? GuardianId,
    UserId? SiblingChildId,
    string? PlaydateHostName,
    string? PlaydateLocation,
    string? PlaydateContactInfo,
    TimeOnly? Time,
    string? Notes)
{
    public static AssignPickup FromClaims(
        ClaimsPrincipal principal,
        UserId childId,
        DateOnly date,
        PickupSlot slot,
        PickupAssigneeKind kind,
        UserId? guardianId,
        UserId? siblingChildId,
        string? playdateHostName,
        string? playdateLocation,
        string? playdateContactInfo,
        TimeOnly? time,
        string? notes) =>
        new(principal.GetUserId(), childId, date, slot, kind, guardianId, siblingChildId, playdateHostName, playdateLocation, playdateContactInfo, time, notes);
}
