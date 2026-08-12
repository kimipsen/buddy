namespace buddy.Features.Users;

public sealed record User(
    Guid Id,
    string KeycloakSubject,
    string? Email,
    string? UserName,
    string? DisplayName);
