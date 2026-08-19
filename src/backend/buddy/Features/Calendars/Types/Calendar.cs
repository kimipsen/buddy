using System.Collections.Immutable;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record Calendar(
    CalendarId Id,
    string Name,
    ImmutableDictionary<UserId, CalendarRole> Members,
    bool IsDeleted = false)
{
    public bool CanView(UserId userId) => !IsDeleted && Members.ContainsKey(userId);

    public bool CanContribute(UserId userId) =>
        !IsDeleted && Members.TryGetValue(userId, out var role) && role is CalendarRole.Owner or CalendarRole.Contributor;

    public bool IsOwner(UserId userId) =>
        !IsDeleted && Members.TryGetValue(userId, out var role) && role == CalendarRole.Owner;

    public static Calendar? Rehydrate(IEnumerable<CalendarEvent> events)
    {
        Calendar? calendar = null;

        foreach (var @event in events)
        {
            calendar = @event switch
            {
                CalendarCreated created => new Calendar(
                    created.CalendarId,
                    created.Name,
                    ImmutableDictionary<UserId, CalendarRole>.Empty.Add(created.OwnerId, CalendarRole.Owner)),
                MemberRoleGranted granted => calendar! with { Members = calendar!.Members.SetItem(granted.MemberId, granted.Role) },
                MemberRoleRevoked revoked => calendar! with { Members = calendar!.Members.Remove(revoked.MemberId) },
                CalendarDeleted => calendar! with { IsDeleted = true },
                _ => calendar
            };
        }

        return calendar;
    }
}
