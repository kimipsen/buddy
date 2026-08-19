namespace buddy.Features.Calendars;

// Owner is assigned only by CalendarCreated -- it is never granted, changed, or revoked through
// MemberRoleGranted/MemberRoleRevoked, so ownership never transfers.
public enum CalendarRole
{
    Owner,
    Contributor,
    Viewer
}
