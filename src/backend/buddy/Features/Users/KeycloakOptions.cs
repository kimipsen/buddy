using System.ComponentModel.DataAnnotations;

namespace buddy.Features.Users;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";

    [Required]
    public required string Authority { get; init; }

    // Empty is allowed: it turns audience validation off (see UsersFeature).
    [Required(AllowEmptyStrings = true)]
    public required string Audience { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;

    // Keycloak's token `iss` claim reflects whatever host/port the client used to reach it, which
    // can differ from Authority (used here purely for JWKS discovery, e.g. a docker-network host).
    // Falls back to Authority when not set.
    public string? ValidIssuer { get; init; }
}
