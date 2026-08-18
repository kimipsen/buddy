namespace buddy.Features.Users
{
    public union UserEvent(
        UserCreated,
        UserDeleted,
        NameUpdated,
        EmailUpdated,
        EmailVerified
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
            EmailVerified e => e,
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
            EmailVerified => nameof(EmailVerified),
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

    public sealed record EmailVerified(UserId UserId, DateTimeOffset OccurredAt);

}
