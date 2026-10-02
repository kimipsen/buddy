using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Time is optional -- a guardian can record "pickup at 15:15 today, early dismissal" for
// precision, but it isn't required to make an assignment meaningful; PickupSlot already conveys
// "morning" vs. "afternoon" on its own. Notes is free text, "" meaning none.
public sealed record PickupAssignment(PickupAssignee Assignee, TimeOnly? Time, UserId AssignedBy, string Notes);
