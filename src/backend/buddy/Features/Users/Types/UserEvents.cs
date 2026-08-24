namespace buddy.Features.Users
{
    using buddy.Features.Calendars;

    public union UserEvent(
        UserCreated,
        UserDeleted,
        NameUpdated,
        EmailUpdated,
        EmailVerificationRequested,
        EmailVerified,
        TimeZoneUpdated,
        LanguageUpdated
    )
    {
        // Marten hands back the deserialized concrete event object when reading a stream; case
        // records convert implicitly back to the union.
        public static UserEvent FromPayload(object payload) => payload switch
        {
            UserCreated e => e,
            UserDeleted e => e,
            NameUpdated e => e,
            EmailUpdated e => e,
            EmailVerificationRequested e => e,
            EmailVerified e => e,
            TimeZoneUpdated e => e,
            LanguageUpdated e => e,
            _ => throw new ArgumentException($"Unknown user event payload: {payload.GetType().Name}", nameof(payload)),
        };

        // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
        // UserEvent returns "UserEvent" for every case -- use this instead.
        public string EventType => this switch
        {
            UserCreated => nameof(UserCreated),
            UserDeleted => nameof(UserDeleted),
            NameUpdated => nameof(NameUpdated),
            EmailUpdated => nameof(EmailUpdated),
            EmailVerificationRequested => nameof(EmailVerificationRequested),
            EmailVerified => nameof(EmailVerified),
            TimeZoneUpdated => nameof(TimeZoneUpdated),
            LanguageUpdated => nameof(LanguageUpdated),
        };
    }

    public sealed record UserCreated(
        UserId UserId,
        KeycloakSubject KeycloakSubject,
        Email Email,
        string? UserName,
        Name Name,
        DateTimeOffset OccurredAt);

    public sealed record UserDeleted(UserId UserId, DateTimeOffset OccurredAt);

    public sealed record NameUpdated(UserId UserId, Name Before, Name After, DateTimeOffset OccurredAt);

    public sealed record EmailUpdated(UserId UserId, Email Before, Email After, DateTimeOffset OccurredAt);

    // TokenHash is a SHA-256 hash of the plaintext token, never the token itself -- the event
    // stream is append-only and gets backed up/replicated, so a bare secret in it could never
    // be revoked or purged. The plaintext is only ever held in memory long enough to email it.
    public sealed record EmailVerificationRequested(UserId UserId, string TokenHash, DateTimeOffset ExpiresAt, DateTimeOffset OccurredAt);

    public sealed record EmailVerified(UserId UserId, DateTimeOffset OccurredAt);

    // No initial value is captured on UserCreated -- a user with no TimeZoneUpdated event yet
    // implicitly defaults to UTC (see User.Rehydrate), the same "sparse log" convention Medicines'
    // DoseLog already uses, so this stays additive over the existing UserCreated event shape.
    public sealed record TimeZoneUpdated(UserId UserId, TimeZoneId Before, TimeZoneId After, DateTimeOffset OccurredAt);

    // No initial value is captured on UserCreated -- a user with no LanguageUpdated event yet
    // implicitly defaults to English (see User.Rehydrate). GetOrCreateUserHandler appends one
    // right after UserCreated when the browser's Accept-Language header resolves to a different
    // supported language, the same way it conditionally appends EmailVerificationRequested.
    public sealed record LanguageUpdated(UserId UserId, Language Before, Language After, DateTimeOffset OccurredAt);

    public sealed record UserEventEntry(long Version, UserEvent Event);

}
