namespace buddy.Features.Users;

public sealed record UserResponse(
    Guid Id,
    string KeycloakSubject,
    string? Email,
    string? UserName,
    string? DisplayName);
