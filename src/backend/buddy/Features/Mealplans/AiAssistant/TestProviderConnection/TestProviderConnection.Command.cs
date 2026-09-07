using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// ApiKey is null to test the already-stored key for this provider, or set to test a key before
// it's ever saved (the settings UI's "Test connection" button covers both moments).
public sealed record TestProviderConnection(UserId? UserId, UserId ChildId, AiProvider Provider, string? ApiKey)
{
    public static TestProviderConnection FromClaims(ClaimsPrincipal principal, UserId childId, AiProvider provider, string? apiKey) =>
        new(principal.GetUserId(), childId, provider, apiKey);
}
