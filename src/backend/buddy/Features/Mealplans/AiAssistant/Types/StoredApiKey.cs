using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// CipherText is produced by IApiKeyCipher -- the plaintext key is never held anywhere past the
// handler call that receives it. Last4 lets the settings UI render "sk-...ab12" without ever
// re-exposing (or re-decrypting for display purposes) the full key.
public sealed record StoredApiKey(string CipherText, string Last4, UserId AddedBy, DateTimeOffset AddedAt)
{
    public static StoredApiKey Create(string plainTextApiKey, IApiKeyCipher cipher, UserId addedBy, DateTimeOffset addedAt) =>
        new(cipher.Protect(plainTextApiKey), Last4Of(plainTextApiKey), addedBy, addedAt);

    private static string Last4Of(string apiKey) => apiKey.Length <= 4 ? apiKey : apiKey[^4..];
}
