using System.Text.Json;

namespace buddy.Features.Guardians;

// Client-credentials grant for the confidential buddy-admin-cli client's own service account -- the
// production equivalent of BuddyApiFixture's test-only admin-cli password grant. Shared by every
// class that calls Keycloak's Admin API (KeycloakAdminClient, the MCP client registry). No token
// caching: acceptable at this call volume (a child provisioned, a daily cleanup pass), noted as a
// follow-up rather than a correctness gap.
internal static class KeycloakAdminToken
{
    public static async Task<string> GetServiceAccountTokenAsync(HttpClient httpClient, KeycloakAdminOptions admin, ILogger logger, CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = admin.ClientId,
            ["client_secret"] = admin.ClientSecret
        });
        using var response = await httpClient.PostAsync(admin.TokenEndpoint, form, cancellationToken);

        // Logged before throwing, like KeycloakAdminClient's other calls: the exception only carries the status.
        if (!response.IsSuccessStatusCode)
        {
            logger.KeycloakAdminRequestFailed("service account token", (int)response.StatusCode);
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak admin token response had no access_token.");
    }
}
