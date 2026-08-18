namespace buddy.Features.Users;

public static class Claims
{
    public const string KeycloakSubject = "sub";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string PreferredUsername = "preferred_username";
    public const string GivenName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";
    public const string FamilyName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";
}