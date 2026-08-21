namespace buddy.Features.Users;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";

    public required string Authority { get; init; }

    public required string Audience { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;

    // Keycloak's token `iss` claim reflects whatever host/port the client used to reach it, which
    // can differ from Authority (used here purely for JWKS discovery, e.g. a docker-network host).
    // Falls back to Authority when not set.
    public string? ValidIssuer { get; init; }
}
