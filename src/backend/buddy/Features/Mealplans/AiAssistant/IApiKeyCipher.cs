namespace buddy.Features.Mealplans;

// A provider API key must never reach an event payload, a log line, or a response as plaintext --
// this is the single seam every write/read of a stored key goes through, so encryption can't be
// forgotten at a new call site (see DataProtectionApiKeyCipher).
public interface IApiKeyCipher
{
    string Protect(string apiKey);

    string Unprotect(string cipherText);
}
