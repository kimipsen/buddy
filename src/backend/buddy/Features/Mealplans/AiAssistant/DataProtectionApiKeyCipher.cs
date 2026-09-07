using Microsoft.AspNetCore.DataProtection;

namespace buddy.Features.Mealplans;

// Framework-provided encryption at rest (no new dependency) -- the purpose string is versioned so
// a future re-key/rotation can introduce ".v2" without being able to decrypt old ciphertext under
// the new purpose by accident.
public sealed class DataProtectionApiKeyCipher(IDataProtectionProvider provider) : IApiKeyCipher
{
    private const string Purpose = "buddy.Mealplans.AiAssistant.ApiKey.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string apiKey) => _protector.Protect(apiKey);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
