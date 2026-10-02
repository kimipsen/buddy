using buddy.Common.Aggregates;
using buddy.Features.Calendars;

namespace buddy.Features.Users;

public sealed record User(
    UserId Id,
    KeycloakSubject KeycloakSubject,
    Email Email,
    string UserName,
    Name Name,
    TimeZoneId TimeZoneId,
    Language Language,
    EmailVerification EmailVerification,
    bool IsDeleted)
{
    public static User? Rehydrate(IEnumerable<UserEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static User Replay(IEnumerable<UserEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so UserSnapshotProjection can
    // drive the same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and User is that
    // document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static User Start(UserEvent @event) => @event switch
    {
        UserCreated created => new User(
            created.UserId,
            created.KeycloakSubject,
            created.Email,
            created.UserName,
            created.Name,
            created.TimeZoneId,
            created.Language,
            new EmailVerification.None(),
            IsDeleted: false),
        _ => throw EventReplay.NotAStartEvent(nameof(User), @event.EventType)
    };

    public static User Advance(User user, UserEvent @event) => @event switch
    {
        NameUpdated nameUpdated => user with { Name = nameUpdated.After },
        TimeZoneUpdated timeZoneUpdated => user with { TimeZoneId = timeZoneUpdated.After },
        LanguageUpdated languageUpdated => user with { Language = languageUpdated.After },
        // A new address is never covered by a verification of the old one, so any
        // pending verification for the old address is cleared here too.
        EmailUpdated emailUpdated => user with { Email = emailUpdated.After, EmailVerification = new EmailVerification.None() },
        EmailVerificationRequested requested => user with
        {
            EmailVerification = new EmailVerification.Pending(requested.TokenHash, requested.OccurredAt, requested.ExpiresAt)
        },
        EmailVerified => user with { Email = user.Email with { IsVerified = true }, EmailVerification = new EmailVerification.None() },
        UserDeleted => user with { IsDeleted = true },
        UserCreated => throw EventReplay.AlreadyStarted(nameof(User), @event.EventType)
    };
}
