namespace buddy.Features.Users;

public union UserEvent(
    UserCreated,
    UserDeleted,
    NameUpdated,
    EmailUpdated,
    EmailVerified
);

public sealed record UserCreated(
    UserId UserId,
    string KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name,
    DateTimeOffset OccurredAt);

public sealed record UserDeleted(DateTimeOffset OccurredAt);

public sealed record NameUpdated(Name Before, Name After, DateTimeOffset OccurredAt);

public sealed record EmailUpdated(Email Before, Email After, DateTimeOffset OccurredAt);

public sealed record EmailVerified(DateTimeOffset OccurredAt);