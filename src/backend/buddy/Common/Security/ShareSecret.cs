using System.Security.Cryptography;
using System.Text;

namespace buddy.Common.Security;

// The secret behind every link that works without a Buddy account: the calendar and meal plan iCal
// feeds and the sleep diary, feelings and visit summary share links. 32 random bytes as base64url;
// only the uppercase hex SHA-256 is ever persisted, so a leaked database can't open a link.
// Changing either function invalidates every link already handed out.
public static class ShareSecret
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
