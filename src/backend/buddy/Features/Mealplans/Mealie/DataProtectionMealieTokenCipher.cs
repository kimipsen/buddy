using Microsoft.AspNetCore.DataProtection;

namespace buddy.Features.Mealplans;

// The Mealie API token's own seam. It derives from IApiKeyCipher so StoredApiKey.Create takes it,
// but it's a separate service with its own Data Protection purpose: an AI key's ciphertext can't
// be unprotected as a Mealie token, or the other way round.
public interface IMealieTokenCipher : IApiKeyCipher;

public sealed class DataProtectionMealieTokenCipher(IDataProtectionProvider provider) : IMealieTokenCipher
{
    private const string Purpose = "buddy.Mealplans.Mealie.Token.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string apiKey) => _protector.Protect(apiKey);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
