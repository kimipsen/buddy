using buddy.Features.Calendars;

namespace buddy.Features.Users;

public sealed record User(
    UserId Id,
    KeycloakSubject KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name,
    bool IsDeleted = false,
    string? EmailVerificationTokenHash = null,
    DateTimeOffset? EmailVerificationRequestedAt = null,
    DateTimeOffset? EmailVerificationExpiresAt = null,
    // No TimeZoneUpdated event yet implicitly means UTC -- see the comment on that event.
    TimeZoneId? TimeZoneId = null)
{
    public TimeZoneId ResolvedTimeZoneId => TimeZoneId ?? Calendars.TimeZoneId.New("UTC");

    public static User? Rehydrate(IEnumerable<UserEvent> events)
    {
        User? user = null;

        foreach (var @event in events)
        {
            user = @event switch
            {
                UserCreated created => new User(
                    created.UserId,
                    created.KeycloakSubject,
                    created.Email,
                    created.UserName,
                    created.Name),
                NameUpdated nameUpdated => user! with { Name = nameUpdated.After },
                TimeZoneUpdated timeZoneUpdated => user! with { TimeZoneId = timeZoneUpdated.After },
                // A new address is never covered by a verification of the old one, so any
                // pending verification for the old address is cleared here too.
                EmailUpdated emailUpdated => user! with
                {
                    Email = emailUpdated.After,
                    EmailVerificationTokenHash = null,
                    EmailVerificationRequestedAt = null,
                    EmailVerificationExpiresAt = null
                },
                EmailVerificationRequested requested => user! with
                {
                    EmailVerificationTokenHash = requested.TokenHash,
                    EmailVerificationRequestedAt = requested.OccurredAt,
                    EmailVerificationExpiresAt = requested.ExpiresAt
                },
                EmailVerified => user! with
                {
                    Email = user!.Email with { IsVerified = true },
                    EmailVerificationTokenHash = null,
                    EmailVerificationRequestedAt = null,
                    EmailVerificationExpiresAt = null
                },
                UserDeleted => user! with { IsDeleted = true },
                _ => user
            };
        }

        return user;
    }
}
