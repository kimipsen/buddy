using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

using buddy.Features.Guardians;

using Microsoft.Extensions.Options;

namespace buddy.Features.Mcp;

// Calls the realm's Admin API as the buddy-admin-cli service account, which needs manage-clients (list,
// stamp, delete) next to manage-users (consents hang off the user resource).
public sealed class KeycloakClientRegistry(
    HttpClient httpClient,
    IOptionsMonitor<KeycloakAdminOptions> options,
    ILogger<KeycloakClientRegistry> logger) : IKeycloakClientRegistry
{
    // The client attribute holding the time the cleanup first saw an unconsented registered client.
    // Keycloak keeps no creation time for a client, and the registration response's client_id_issued_at
    // is not stored; an attribute needs no Buddy-side storage and goes away with the client.
    public const string FirstSeenAttribute = "buddy.firstSeen";

    private const int PageSize = 100;

    private static readonly string[] ScopeProperties = ["defaultClientScopes", "optionalClientScopes"];

    public async Task<IReadOnlyList<KeycloakClientSummary>> ListClientsAsync(CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);

        var clients = new List<KeycloakClientSummary>();
        foreach (var client in await GetAllPagesAsync(admin, token, "clients", "list clients", cancellationToken))
        {
            var clientId = client["clientId"]?.GetValue<string>();
            var id = client["id"]?.GetValue<string>();
            if (clientId is null || id is null)
            {
                continue;
            }

            var scopes = ScopeProperties
                .SelectMany(property => client[property] as JsonArray ?? [])
                .Select(scope => scope?.GetValue<string>())
                .OfType<string>()
                .ToList();

            clients.Add(new KeycloakClientSummary(
                id,
                clientId,
                client["publicClient"]?.GetValue<bool>() ?? false,
                scopes,
                client["attributes"]?[FirstSeenAttribute]?.GetValue<string>()));
        }

        return clients;
    }

    public async Task<int> CountUsersAsync(CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);

        using var request = Authorized(HttpMethod.Get, $"{admin.AdminBaseUrl}/users/count", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureSuccess(response, "count users");

        return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListUserIdsAsync(CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);

        return (await GetAllPagesAsync(admin, token, "users", "list users", cancellationToken))
            .Select(user => user["id"]?.GetValue<string>() ?? throw new InvalidOperationException("Keycloak returned a user without an id."))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListConsentedClientIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);

        using var request = Authorized(HttpMethod.Get, $"{admin.AdminBaseUrl}/users/{Uri.EscapeDataString(userId)}/consents", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // The user was deleted since it was listed: its consents went with it.
            return [];
        }

        EnsureSuccess(response, "list user consents");

        var consents = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty consents answer.");

        return consents
            .Select(consent => consent?["clientId"]?.GetValue<string>() ?? throw new InvalidOperationException("Keycloak returned a consent without a clientId."))
            .ToList();
    }

    public async Task SetClientAttributeAsync(string id, string name, string value, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);
        var clientUrl = $"{admin.AdminBaseUrl}/clients/{Uri.EscapeDataString(id)}";

        using var getRequest = Authorized(HttpMethod.Get, clientUrl, token);
        using var getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
        if (getResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(getResponse, "read client");

        var client = await getResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty client representation.");

        // Read, merge, write: the PUT replaces the attributes map, so sending only this one would drop the
        // client's PKCE setting, consent text and the rest.
        var attributes = client["attributes"] as JsonObject ?? [];
        attributes[name] = value;
        client["attributes"] = attributes;

        using var putRequest = Authorized(HttpMethod.Put, clientUrl, token);
        putRequest.Content = JsonContent.Create(client);

        using var putResponse = await httpClient.SendAsync(putRequest, cancellationToken);
        EnsureSuccess(putResponse, "set client attribute");
    }

    public async Task DeleteClientAsync(string id, CancellationToken cancellationToken)
    {
        var admin = options.CurrentValue;
        var token = await KeycloakAdminToken.GetServiceAccountTokenAsync(httpClient, admin, logger, cancellationToken);

        using var request = Authorized(HttpMethod.Delete, $"{admin.AdminBaseUrl}/clients/{Uri.EscapeDataString(id)}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(response, "delete client");
    }

    // Keycloak pages with first/max and has no total; a short page is the last one.
    private async Task<List<JsonNode>> GetAllPagesAsync(KeycloakAdminOptions admin, string token, string collection, string operation, CancellationToken cancellationToken)
    {
        var all = new List<JsonNode>();
        for (var first = 0; ; first += PageSize)
        {
            using var request = Authorized(HttpMethod.Get, $"{admin.AdminBaseUrl}/{collection}?first={first}&max={PageSize}", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            EnsureSuccess(response, operation);

            var page = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken) ?? [];
            all.AddRange(page.Where(item => item is not null).Select(item => item!));

            if (page.Count < PageSize)
            {
                return all;
            }
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // The same shape as KeycloakAdminClient.EnsureSuccess: log which call failed, then throw.
    private void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.KeycloakAdminRequestFailed(operation, (int)response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
    }
}
