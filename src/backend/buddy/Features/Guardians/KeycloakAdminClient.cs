using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using buddy.Features.Users;

using Microsoft.Extensions.Options;

namespace buddy.Features.Guardians;

public sealed class KeycloakAdminClient(HttpClient httpClient, IOptionsMonitor<KeycloakAdminOptions> options) : IKeycloakAdminClient
{
    public async Task<KeycloakProvisionedUser> CreateChildUserAsync(string displayName, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await GetServiceAccountTokenAsync(admin, cancellationToken);

        // Keycloak requires a username even though the child has no email; it's an opaque handle,
        // never shown to anyone, so a random one is fine -- the guardian identifies the child by
        // Name on our own User aggregate, not by this Keycloak username.
        var username = $"child.{Guid.NewGuid():N}";
        var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{admin.AdminBaseUrl}/users")
        {
            Content = JsonContent.Create(new
            {
                username,
                enabled = true,
                firstName = displayName,
                requiredActions = new[] { "UPDATE_PASSWORD" },
                credentials = new[]
                {
                    new { type = "password", value = temporaryPassword, temporary = true }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");
        var subject = location.Segments[^1];

        return new KeycloakProvisionedUser(new KeycloakSubject(subject), username, temporaryPassword);
    }

    // Client-credentials grant for this confidential client's own service account -- the
    // production equivalent of BuddyApiFixture's test-only admin-cli password grant. No token
    // caching in v1: acceptable at this call volume (one call per child provisioned), noted as a
    // follow-up rather than a correctness gap.
    private async Task<string> GetServiceAccountTokenAsync(KeycloakAdminOptions admin, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(admin.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = admin.ClientId,
            ["client_secret"] = admin.ClientSecret
        }), cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak admin token response had no access_token.");
    }
}
