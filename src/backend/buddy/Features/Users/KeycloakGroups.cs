using System.Security.Claims;

namespace buddy.Features.Users;

// The Keycloak groups that let an account use Buddy at all. Keycloak puts the account's group
// names (not paths: the mapper's full.path is false) in the token's "groups" claim, via the
// "groups" protocol mapper on the frontend client (.devcontainer/keycloak/buddy-realm.json).
// Guardians are put in buddy-guardian by hand in Keycloak; children are put in buddy-child by
// KeycloakAdminClient when a guardian creates them.
public static class KeycloakGroups
{
    public const string ClaimType = "groups";
    public const string Guardian = "buddy-guardian";
    public const string Child = "buddy-child";

    public static bool IsBuddyMember(this ClaimsPrincipal principal) =>
        principal.HasClaim(ClaimType, Guardian) || principal.HasClaim(ClaimType, Child);
}
