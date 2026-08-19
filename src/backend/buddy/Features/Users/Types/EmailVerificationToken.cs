using System.Security.Cryptography;
using System.Text;

namespace buddy.Features.Users;

public static class EmailVerificationToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

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
