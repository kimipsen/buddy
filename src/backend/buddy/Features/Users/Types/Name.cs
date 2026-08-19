namespace buddy.Features.Users;

public sealed record Name(string GivenName, string FamilyName)
{
    public static Name New(string givenName, string familyName) => new(givenName, familyName);
};