namespace buddy.Features.Users;

public sealed record Email(string Value, bool IsVerified)
{
    public static Email Verified(string value) => new(value, true);
    public static Email Unverified(string value) => new(value, false);
}
