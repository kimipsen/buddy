namespace buddy.Features.Users;

public sealed record UserCreated(
    Guid UserId,
    string KeycloakSubject,
    string? Email,
    string? UserName,
    string? DisplayName,
    DateTimeOffset OccurredAt);
