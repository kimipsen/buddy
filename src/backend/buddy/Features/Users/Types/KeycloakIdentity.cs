namespace buddy.Features.Users;

// Maps a Keycloak subject to its Buddy user. Deleted is set together with UserDeleted: from then on
// the subject gets no UserId claim (UserIdClaimsTransformation) and is never provisioned again
// (GetOrCreateUser), even while its Keycloak account still exists. See gdpr-data-protection.md.
public sealed record KeycloakIdentity(string Id, UserId UserId, bool Deleted = false);
