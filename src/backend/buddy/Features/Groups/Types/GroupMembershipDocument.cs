namespace buddy.Features.Groups;

// Queryable read-model index kept alongside the Group event stream, the same pattern as
// CalendarMembershipDocument for Calendars. GroupName is safe to cache here because nothing in
// this feature renames a group.
public sealed record GroupMembershipDocument(string Id, Guid GroupId, Guid UserId, GroupRole Role, string GroupName)
{
    public static string BuildId(Guid groupId, Guid userId) => $"{groupId}:{userId}";
}
