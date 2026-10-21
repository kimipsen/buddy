using System.Security.Cryptography;

using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// The family's live Mealie connection, decrypted for one handler call. The token never leaves the
// MealieEndpoint it is put in.
public sealed record ActiveMealieConnection(MealieConnectionId Id, MealieConnectionDetails Details, MealieEndpoint Endpoint);

public static class MealieConnectionResolution
{
    // Call after authorization: NotConnected when the family has no stream or is disconnected.
    public static async Task<MealieOutcome<ActiveMealieConnection>> ResolveAsync(
        UserId childId,
        IGuardianLinkEventStore guardians,
        IMealieConnectionEventStore connections,
        IMealieTokenCipher cipher,
        CancellationToken cancellationToken)
    {
        var id = await MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync(childId, guardians, connections, cancellationToken);
        var connection = id is null ? null : await connections.FindSnapshotAsync(id, cancellationToken);

        if (id is null || connection?.Current is not { } current)
        {
            return new MealieOutcome<ActiveMealieConnection>.NotConnected();
        }

        string token;

        try
        {
            token = cipher.Unprotect(current.Token.CipherText);
        }
        catch (CryptographicException)
        {
            // The Data Protection key ring lost the key the token was stored under: the guardian
            // has to paste the token again, the same fix as for a revoked one.
            return new MealieOutcome<ActiveMealieConnection>.UpstreamFailed(new MealieFailure(
                MealieFailureKind.TokenRejected, "Buddy can no longer read the stored Mealie token. Reconnect Mealie under Settings."));
        }

        var endpoint = new MealieEndpoint(current.BaseUrl, token);

        return new MealieOutcome<ActiveMealieConnection>.Success(new ActiveMealieConnection(id, current, endpoint));
    }

    // Re-raises a failed outcome with another payload type (see ResultExtensions.Reraise).
    public static MealieOutcome<TOther> Reraise<T, TOther>(this MealieOutcome<T> outcome) => outcome switch
    {
        MealieOutcome<T>.NotFound => new MealieOutcome<TOther>.NotFound(),
        MealieOutcome<T>.Forbidden => new MealieOutcome<TOther>.Forbidden(),
        MealieOutcome<T>.Validation(var problem) => new MealieOutcome<TOther>.Validation(problem),
        MealieOutcome<T>.NotConnected => new MealieOutcome<TOther>.NotConnected(),
        MealieOutcome<T>.UpstreamFailed(var failure) => new MealieOutcome<TOther>.UpstreamFailed(failure),
        MealieOutcome<T>.Success => throw new InvalidOperationException("Reraise called with a successful outcome."),
    };

    // The success value mapped to a response shape; every other case carried over.
    public static MealieOutcome<TOther> Map<T, TOther>(this MealieOutcome<T> outcome, Func<T, TOther> map) =>
        outcome is MealieOutcome<T>.Success(var value)
            ? new MealieOutcome<TOther>.Success(map(value))
            : outcome.Reraise<T, TOther>();

    // "https://mealie.example.com/g/home/r/lasagne" -- the recipe in Mealie's own UI.
    public static string RecipeWebUrl(MealieConnectionDetails details, string slug) =>
        $"{details.BaseUrl}/g/{Uri.EscapeDataString(details.Household.GroupSlug)}/r/{Uri.EscapeDataString(slug)}";
}
