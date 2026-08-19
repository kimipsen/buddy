using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarEvent(
    CalendarCreated,
    CalendarDeleted,
    MemberRoleGranted,
    MemberRoleRevoked
)
{
    public static CalendarEvent FromPayload(object payload) => payload switch
    {
        CalendarCreated e => e,
        CalendarDeleted e => e,
        MemberRoleGranted e => e,
        MemberRoleRevoked e => e,
        _ => throw new ArgumentException($"Unknown calendar event payload: {payload.GetType().Name}", nameof(payload)),
    };

    // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
    // CalendarEvent returns "CalendarEvent" for every case -- use this instead.
    public string EventType => this switch
    {
        CalendarCreated => nameof(CalendarCreated),
        CalendarDeleted => nameof(CalendarDeleted),
        MemberRoleGranted => nameof(MemberRoleGranted),
        MemberRoleRevoked => nameof(MemberRoleRevoked),
    };
}

public sealed record CalendarCreated(CalendarId CalendarId, UserId OwnerId, string Name, DateTimeOffset OccurredAt);

public sealed record CalendarDeleted(CalendarId CalendarId, UserId DeletedBy, DateTimeOffset OccurredAt);

// Role is always Contributor or Viewer -- Owner is assigned only by CalendarCreated.
public sealed record MemberRoleGranted(CalendarId CalendarId, UserId MemberId, CalendarRole Role, UserId GrantedBy, DateTimeOffset OccurredAt);

public sealed record MemberRoleRevoked(CalendarId CalendarId, UserId MemberId, UserId RevokedBy, DateTimeOffset OccurredAt);
