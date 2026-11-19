namespace buddy.Features.Mcp;

// What the MCP client cleanup needs from Keycloak's Admin API about the realm's OAuth clients. Kept apart
// from IKeycloakAdminClient (user provisioning): different permissions (manage-clients next to manage-users)
// and a different caller, a daily background job. Every call throws on a failure or an answer it can't read;
// McpClientCleaner turns that into "keep the client", never into a deletion.
public interface IKeycloakClientRegistry
{
    // Every client of the realm: Keycloak's built-ins, Buddy's own and the dynamically registered ones.
    Task<IReadOnlyList<KeycloakClientSummary>> ListClientsAsync(CancellationToken cancellationToken);

    // GET /users/count: the number of users the paged list must add up to.
    Task<int> CountUsersAsync(CancellationToken cancellationToken);

    // The ids of every user, read page by page.
    Task<IReadOnlyList<string>> ListUserIdsAsync(CancellationToken cancellationToken);

    // The clientId of every client this user has consented to. Keycloak stores consents per user.
    Task<IReadOnlyList<string>> ListConsentedClientIdsAsync(string userId, CancellationToken cancellationToken);

    // Sets one attribute on the client: reads the client, merges the attribute into its attributes and
    // writes the whole representation back, because a client update replaces what it doesn't mention.
    Task SetClientAttributeAsync(string id, string name, string value, CancellationToken cancellationToken);

    // Idempotent: a client that is already gone counts as deleted.
    Task DeleteClientAsync(string id, CancellationToken cancellationToken);
}

// Id is Keycloak's internal id, ClientId the OAuth client_id. For a dynamically registered client both are
// the same UUID; for a client created by hand ClientId is readable. Scopes are the default and optional
// client scope names; FirstSeen is the raw buddy.firstSeen attribute (null when absent).
public sealed record KeycloakClientSummary(string Id, string ClientId, bool PublicClient, IReadOnlyList<string> Scopes, string? FirstSeen);
