namespace buddy.Features.Guardians;

// Config for the confidential-client service account used to call Keycloak's Admin API when a
// guardian provisions a child account. Deliberately separate from Features/Users/KeycloakOptions.cs
// (RP-side token validation only) -- this authenticates the backend itself, not an end user, and
// nothing like it existed before this feature. ClientSecret must come from real secret storage in
// production (see the skill's secrets guidance); appsettings.Development.json only holds a local
// dev placeholder.
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Authentication:KeycloakAdmin";

    public required string Realm { get; init; }

    public required string TokenEndpoint { get; init; }

    // Base URL for this realm's Admin REST API, e.g. "http://keycloak:8080/admin/realms/buddy".
    public required string AdminBaseUrl { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}
