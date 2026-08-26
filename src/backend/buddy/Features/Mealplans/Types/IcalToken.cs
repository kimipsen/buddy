using System.Security.Cryptography;
using System.Text;

namespace buddy.Features.Mealplans;

// Mirrors Features/Calendars/Types/IcalToken.cs -- only the hash is ever persisted, never the
// plaintext token. No expiry: an ics subscription link is meant to stay valid indefinitely until
// the owner explicitly revokes it.
public static class IcalToken
{
    private const int TokenSizeInBytes = 32;

    public static (string Token, string Hash) Generate()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeInBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
