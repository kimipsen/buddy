namespace buddy.Features.Users;

public sealed record UserResponse(
    UserId Id,
    KeycloakSubject KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name);
