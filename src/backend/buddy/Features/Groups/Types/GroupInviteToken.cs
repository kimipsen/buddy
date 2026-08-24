using System.Security.Cryptography;
using System.Text;

namespace buddy.Features.Groups;

// Mirrors Users' EmailVerificationToken exactly. Kept feature-local rather than shared cross-
// feature, consistent with how Users/Groups/Guardians each own their types.
public static class GroupInviteToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private const int TokenSizeInBytes = 32;

    public static (string Token, string Hash, DateTimeOffset ExpiresAt) Generate(DateTimeOffset now)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeInBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (token, Hash(token), now.Add(Lifetime));
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
