namespace buddy.Features.Calendars;

// Owner comes only from the owning group's CalendarPermissionPolicy -- it is never granted,
// changed, or revoked through MemberRoleGranted/MemberRoleRevoked.
public enum CalendarRole
{
    Owner,
    Contributor,
    Viewer
}
