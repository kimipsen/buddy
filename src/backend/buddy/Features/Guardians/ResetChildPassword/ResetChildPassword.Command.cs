using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// Gives a child a new one-time password. A child has no email, so Keycloak's own "forgot password"
// flow can't reach them; their guardian resets it here instead. Any active guardian of the child
// may, the same tier that can create one.
public sealed record ResetChildPassword(UserId GuardianId, UserId ChildId)
{
    public static ResetChildPassword FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}

public union ResetChildPasswordOutcome(ResetChildPasswordOutcome.Success, ResetChildPasswordOutcome.NotFound)
{
    public sealed record Success(string Username, string TemporaryPassword);
    public sealed record NotFound;
}
