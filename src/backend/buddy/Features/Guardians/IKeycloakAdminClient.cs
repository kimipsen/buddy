using buddy.Features.Users;

namespace buddy.Features.Guardians;

public interface IKeycloakAdminClient
{
    // Creates a Keycloak user with no email (see the analysis doc's "no email required" decision --
    // GetOrCreateUserHandler's existing `?? ""` convention already represents "no email" on User) and
    // a one-time temporary password the guardian is shown once. Returns the new user's `sub` --
    // Keycloak stays the only source of new sub values, same invariant as the existing lazy-
    // materialization flow.
    Task<KeycloakProvisionedUser> CreateChildUserAsync(string displayName, CancellationToken cancellationToken);
}

// Username is the child's login handle in Keycloak -- the guardian needs it alongside
// TemporaryPassword to actually hand the child something they can log in with (the child has no
// email, so username/password is the only login method).
public sealed record KeycloakProvisionedUser(KeycloakSubject Subject, string Username, string TemporaryPassword);
