using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarEvent(
    CalendarCreated,
    CalendarCreatedForGroup,
    CalendarIconChanged,
    CalendarTransferredToGroup,
    CalendarDeleted,
    MemberRoleGranted,
    MemberRoleRevoked,
    IcalTokenIssued,
    IcalTokenRevoked
)
{
    public static CalendarEvent FromPayload(object payload) => payload switch
    {
        CalendarCreated e => e,
        CalendarCreatedForGroup e => e,
        CalendarIconChanged e => e,
        CalendarTransferredToGroup e => e,
        CalendarDeleted e => e,
        MemberRoleGranted e => e,
        MemberRoleRevoked e => e,
        IcalTokenIssued e => e,
        IcalTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown calendar event payload: {payload.GetType().Name}", nameof(payload)),
    };

    // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
    // CalendarEvent returns "CalendarEvent" for every case -- use this instead.
    public string EventType => this switch
    {
        CalendarCreated => nameof(CalendarCreated),
        CalendarCreatedForGroup => nameof(CalendarCreatedForGroup),
        CalendarIconChanged => nameof(CalendarIconChanged),
        CalendarTransferredToGroup => nameof(CalendarTransferredToGroup),
        CalendarDeleted => nameof(CalendarDeleted),
        MemberRoleGranted => nameof(MemberRoleGranted),
        MemberRoleRevoked => nameof(MemberRoleRevoked),
        IcalTokenIssued => nameof(IcalTokenIssued),
        IcalTokenRevoked => nameof(IcalTokenRevoked),
    };
}

public sealed record CalendarCreated(CalendarId CalendarId, UserId OwnerId, string Name, TimeZoneId TimeZoneId, DateTimeOffset OccurredAt);

// Additive sibling to CalendarCreated, used only when a calendar is created for a group instead
// of a user. CalendarCreated itself is never modified -- old streams are read exactly as stored,
// with no upcasting. See docs/backend/analysis/group-owned-calendars-and-permissions.md.
public sealed record CalendarCreatedForGroup(CalendarId CalendarId, GroupId OwnerId, string Name, TimeZoneId TimeZoneId, DateTimeOffset OccurredAt);

// Icon is the only calendar-level detail that can change after creation today -- Name and
// TimeZoneId remain fixed. See Calendar.DefaultIcon for the value assumed until this event first
// appears in a calendar's stream.
public sealed record CalendarIconChanged(CalendarId CalendarId, Icon Icon, UserId ChangedBy, DateTimeOffset OccurredAt);

// The one exception to "ownership is fixed at creation, never transferred" -- a calendar's
// owning group can be changed afterward (e.g. moving a legacy personally-owned calendar into a
// group, or moving it from one group to another), gated by CheckOwner on the calendar itself and
// GroupAuthorization.CheckManage on NewGroupId (two-sided consent, the same shape
// ShareMealPlanWithGroup/ShareMedicineWithGroup already use). Calendar.Members is untouched by a
// transfer -- explicit per-user grants always win over the owning group's policy regardless of
// which group that is, so nobody's access silently changes just because ownership moved.
public sealed record CalendarTransferredToGroup(CalendarId CalendarId, GroupId NewGroupId, UserId TransferredBy, DateTimeOffset OccurredAt);

public sealed record CalendarDeleted(CalendarId CalendarId, UserId DeletedBy, DateTimeOffset OccurredAt);

// Role is always Contributor or Viewer -- Owner is assigned only by CalendarCreated.
public sealed record MemberRoleGranted(CalendarId CalendarId, UserId MemberId, CalendarRole Role, UserId GrantedBy, DateTimeOffset OccurredAt);

public sealed record MemberRoleRevoked(CalendarId CalendarId, UserId MemberId, UserId RevokedBy, DateTimeOffset OccurredAt);

// TokenHash is a SHA-256 hash of the plaintext token, never the token itself -- same reasoning as
// User's EmailVerificationRequested: the event stream is append-only, so a bare secret in it
// could never be revoked or purged.
public sealed record IcalTokenIssued(CalendarId CalendarId, IcalTokenId TokenId, string TokenHash, UserId IssuedBy, DateTimeOffset OccurredAt);

public sealed record IcalTokenRevoked(CalendarId CalendarId, IcalTokenId TokenId, UserId RevokedBy, DateTimeOffset OccurredAt);
