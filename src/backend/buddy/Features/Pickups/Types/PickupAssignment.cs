using buddy.Features.Users;

namespace buddy.Features.Pickups;

// GuardianId/SiblingChildId/Playdate* are only meaningful for their matching Kind -- see
// AssignPickupHandler.ValidateFields, which is where that's enforced at write time; nothing here
// stops a caller from constructing an inconsistent combination directly, so validation is the
// only guard (see Types/PickupAssigneeKind.cs for why this isn't a closed union instead).
// Time is optional -- a guardian can record "pickup at 15:15 today, early dismissal" for
// precision, but it isn't required to make an assignment meaningful; PickupSlot already conveys
// "morning" vs. "afternoon" on its own.
public sealed record PickupAssignment(
    PickupAssigneeKind Kind,
    UserId? GuardianId,
    UserId? SiblingChildId,
    string? PlaydateHostName,
    string? PlaydateLocation,
    string? PlaydateContactInfo,
    TimeOnly? Time,
    UserId AssignedBy,
    string? Notes);
