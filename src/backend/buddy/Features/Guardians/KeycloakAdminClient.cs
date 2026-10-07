using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using buddy.Features.Users;

using Microsoft.Extensions.Options;

namespace buddy.Features.Guardians;

public sealed class KeycloakAdminClient(HttpClient httpClient, IOptionsMonitor<KeycloakAdminOptions> options, ILogger<KeycloakAdminClient> logger) : IKeycloakAdminClient
{
    // Every child account is tagged with this realm role so RP-side/token-based checks can tell a
    // child principal apart from a guardian one without a GuardianLink lookup. Must exist as a
    // realm role in Keycloak already (see Fixtures/TestRealm.json for the test realm) -- this
    // client doesn't create roles, only assigns them.
    private const string ChildRoleName = "buddy-child";

    // The child sets their own password at first login; the generated one is only for that login.
    private static readonly string[] RequiredActionsForNewChild = ["UPDATE_PASSWORD"];

    public async Task<KeycloakCreateUserResult> CreateChildUserAsync(
        string givenName,
        string familyName,
        string username,
        CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await GetServiceAccountTokenAsync(admin, cancellationToken);

        var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{admin.AdminBaseUrl}/users")
        {
            Content = JsonContent.Create(new
            {
                username,
                enabled = true,
                firstName = givenName,
                lastName = familyName,
                requiredActions = RequiredActionsForNewChild,
                credentials = new[]
                {
                    new { type = "password", value = temporaryPassword, temporary = true }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return new KeycloakCreateUserResult.UsernameUnavailable();
        }

        EnsureSuccess(response, "create user");

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");
        var subject = location.Segments[^1];

        await AssignChildRoleAsync(admin, token, subject, cancellationToken);

        return new KeycloakCreateUserResult.Success(
            new KeycloakProvisionedUser(new KeycloakSubject(subject), username, temporaryPassword));
    }

    public async Task DeleteUserAsync(KeycloakSubject subject, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await GetServiceAccountTokenAsync(admin, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{admin.AdminBaseUrl}/users/{Uri.EscapeDataString(subject.Value)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(response, "delete user");
    }

    public async Task MarkEmailVerifiedAsync(KeycloakSubject subject, string email, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await GetServiceAccountTokenAsync(admin, cancellationToken);
        var userUrl = $"{admin.AdminBaseUrl}/users/{Uri.EscapeDataString(subject.Value)}";

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, userUrl);
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
        if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(getResponse, "read user");

        var user = await getResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty user representation.");

        var keycloakEmail = user["email"]?.GetValue<string>();
        var alreadyVerified = user["emailVerified"]?.GetValue<bool>() ?? false;

        if (alreadyVerified || !string.Equals(keycloakEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // PUT the representation Keycloak just returned with only emailVerified changed, rather than
        // a partial body: with the user profile feature (Keycloak 24+), attributes missing from an
        // update can be dropped.
        user["emailVerified"] = true;

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, userUrl)
        {
            Content = JsonContent.Create(user)
        };
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var putResponse = await httpClient.SendAsync(putRequest, cancellationToken);
        EnsureSuccess(putResponse, "mark email verified");
    }

    // Looks up the role via the user's own "available realm roles" list rather than the general
    // /roles/{name} endpoint -- the latter needs "view-realm", but this service account is
    // deliberately scoped to just "manage-users" (see the analysis doc's least-privilege decision),
    // and role-mappings/realm/available is guarded by that same "manage-users" permission since
    // it hangs off the user resource, not the realm-wide roles resource.
    private async Task AssignChildRoleAsync(KeycloakAdminOptions admin, string token, string userId, CancellationToken cancellationToken)
    {
        using var availableRequest = new HttpRequestMessage(HttpMethod.Get, $"{admin.AdminBaseUrl}/users/{userId}/role-mappings/realm/available");
        availableRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var availableResponse = await httpClient.SendAsync(availableRequest, cancellationToken);
        EnsureSuccess(availableResponse, "list available realm roles");

        var availableRoles = await availableResponse.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken) ?? [];
        var childRole = availableRoles.FirstOrDefault(role => role.GetProperty("name").GetString() == ChildRoleName);
        if (childRole.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Keycloak realm has no '{ChildRoleName}' role available to assign.");
        }

        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"{admin.AdminBaseUrl}/users/{userId}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { childRole })
        };
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var assignResponse = await httpClient.SendAsync(assignRequest, cancellationToken);
        EnsureSuccess(assignResponse, "assign child role");
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

        EnsureSuccess(response, "service account token");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak admin token response had no access_token.");
    }

    // Throws like EnsureSuccessStatusCode, after logging which call failed: the exception alone
    // only carries the status code, not whether it was the token, the user or the role mapping.
    private void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.KeycloakAdminRequestFailed(operation, (int)response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
    }
}
