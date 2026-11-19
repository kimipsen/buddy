using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// The max-clients policy against the real Keycloak, in a throwaway realm made from TestRealm.json with the cap
// patched to the realm's own clients plus 2 (the shipped value, own clients plus 50, is a parity assertion in
// KeycloakParityTests). The realm is deleted in DisposeAsync, so a failed run can't leak one into the shared
// container. Creating a realm is a master-realm capability: the fixture's master admin account does it.
[Collection(BuddyApiCollection.Name)]
public sealed class McpClientRegistrationCapTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const int BuiltInClients = 6;

    private readonly string _realm = $"buddy-cap-{Guid.NewGuid():N}"[..20];

    public async Task InitializeAsync()
    {
        var realm = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestRealm.json")))!;
        realm["realm"] = _realm;
        var own = BuiltInClients + realm["clients"]!.AsArray().Count;
        var maxClients = realm["components"]!["org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy"]!.AsArray()
            .Single(policy => policy!["providerId"]!.GetValue<string>() == "max-clients")!;
        maxClients["config"]!["max-clients"] = new JsonArray((own + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));

        await fixture.CreateRealmAsync(realm.ToJsonString());
    }

    public async Task DisposeAsync() => await fixture.DeleteRealmAsync(_realm);

    private async Task<(HttpStatusCode Status, JsonElement Body)> RegisterAsync(string redirectUri, string scope = "buddy-mcp:read", string authMethod = "none")
    {
        using var response = await fixture.RegisterClientRawAsync(new
        {
            client_name = "Cap test",
            redirect_uris = new[] { redirectUri },
            token_endpoint_auth_method = authMethod,
            scope
        }, _realm);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task The_realm_from_the_test_realm_file_refuses_what_the_policies_refuse_and_stops_at_the_cap()
    {
        // The policies are the same set as the shared realm's, in a realm of their own.
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync("https://evil.example/callback")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync("http://127.0.0.1:1/callback", scope: "buddy-mcp:read offline_access")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync("http://127.0.0.1:1/callback", authMethod: "client_secret_basic")).Status);

        // Two registrations fit under the cap, the third does not.
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync("http://127.0.0.1:1/callback")).Status);
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync("http://127.0.0.1:2/callback")).Status);

        var (status, body) = await RegisterAsync("http://127.0.0.1:3/callback");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("Max Clients", body.GetProperty("error_description").GetString());
    }
}
