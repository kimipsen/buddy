namespace buddy.Features.Users;

public sealed record User(
    UserId Id,
    string KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name);
