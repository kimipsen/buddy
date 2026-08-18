namespace buddy.Features.Users;

public sealed record UserResponse(
    UserId Id,
    string KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name);
