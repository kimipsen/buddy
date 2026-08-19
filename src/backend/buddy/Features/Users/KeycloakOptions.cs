namespace buddy.Features.Users;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";

    public required string Authority { get; init; }

    public required string Audience { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;
}
