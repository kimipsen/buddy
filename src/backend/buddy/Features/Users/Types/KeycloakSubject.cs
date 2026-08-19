namespace buddy.Features.Users;

public sealed record KeycloakSubject(string Value)
{
    public static KeycloakSubject New(string value) => new(value);
}