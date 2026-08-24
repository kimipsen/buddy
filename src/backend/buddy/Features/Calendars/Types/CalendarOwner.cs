using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// The owning principal -- set at creation and stable thereafter, except for the one deliberate
// transfer path: CalendarTransferredToGroup (see Types/CalendarEvents.cs). Distinct from the
// per-user *effective* CalendarRole computed by CalendarAuthorization: a group-owned calendar has
// no single "owner user" in Calendar.Members, ownership is anchored to the group as a whole.
public union CalendarOwner(CalendarOwner.User, CalendarOwner.Group)
{
    public sealed record User(UserId Value);
    public sealed record Group(GroupId Value);
}
