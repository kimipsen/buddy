using System.Net;
using System.Text;
using System.Text.Json;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 4 of docs/backend/analysis/mcp-server.md: what an anonymous, dynamically registered client may be,
// against the real Keycloak container with the test realm's registration policies (the same ones as the app
// realm, KeycloakParityTests). Every refusal is Keycloak's own, so these tests are the proof that the policy
// settings in the realm file do what the analysis says.
[Collection(BuddyApiCollection.Name)]
public sealed class McpClientRegistrationTests(BuddyApiFixture fixture)
{
    private static Dictionary<string, object?> Registration(string redirectUri, string? scope = "buddy-mcp:read", string authMethod = "none", string[]? grantTypes = null, string[]? responseTypes = null) =>
        new Dictionary<string, object?>
        {
            ["client_name"] = "Registration test",
            ["redirect_uris"] = new[] { redirectUri },
            ["token_endpoint_auth_method"] = authMethod,
            ["grant_types"] = grantTypes ?? ["authorization_code", "refresh_token"],
            ["response_types"] = responseTypes ?? ["code"],
            ["scope"] = scope
        }.Where(pair => pair.Value is not null).ToDictionary();

    private async Task<(HttpStatusCode Status, JsonElement Body)> RegisterAsync(object body)
    {
        using var response = await fixture.RegisterClientRawAsync(body);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task A_loopback_client_registers_as_a_public_PKCE_client_that_needs_consent_and_has_no_full_scope()
    {
        var clientId = await fixture.RegisterClientAsync("http://127.0.0.1:41234/callback");

        Assert.True(Guid.TryParse(clientId, out _), "Keycloak assigns a UUID client_id to a dynamically registered client.");

        var client = (await fixture.GetKeycloakClientAsync(clientId))!.Value;
        Assert.True(client.GetProperty("publicClient").GetBoolean());
        Assert.True(client.GetProperty("consentRequired").GetBoolean());
        Assert.False(client.GetProperty("fullScopeAllowed").GetBoolean());
        Assert.False(client.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
        Assert.Equal(["buddy-mcp:read"], client.GetProperty("optionalClientScopes").EnumerateArray().Select(scope => scope.GetString()));
    }

    [Theory]
    [InlineData("http://localhost:8123/oauth/callback")]
    [InlineData("https://claude.ai/api/mcp/auth_callback")]
    [InlineData("https://vscode.dev/redirect")]
    [InlineData("https://chatgpt.com/connector_platform_oauth_redirect")]
    public async Task A_redirect_uri_on_loopback_or_a_known_callback_host_is_accepted(string redirectUri)
    {
        var (status, _) = await RegisterAsync(Registration(redirectUri));

        Assert.Equal(HttpStatusCode.Created, status);
    }

    // Found in the slice 4 spike: the policy can't match an IPv6 literal, with "::1" in its host list or without.
    // A client that only listens on [::1] uses the pre-registered buddy-mcp client, whose redirect URIs include it.
    [Theory]
    [InlineData("http://[::1]:8123/oauth/callback")]
    [InlineData("https://evil.example/callback")]
    [InlineData("https://claude.ai.evil.example/callback")]
    // Hostless schemes would let any app that claims the scheme on the guardian's machine receive the code.
    [InlineData("cursor://anysphere.cursor-retrieval/oauth/callback")]
    public async Task Any_other_redirect_uri_is_refused_by_the_trusted_hosts_policy(string redirectUri)
    {
        var (status, body) = await RegisterAsync(Registration(redirectUri));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("Trusted Hosts", body.GetProperty("error_description").GetString());
    }

    [Theory]
    [InlineData("buddy-mcp:read offline_access")]
    [InlineData("buddy-mcp:read address")]
    [InlineData("buddy-mcp:read kc-admin-api")]
    public async Task A_scope_beyond_buddy_mcp_read_is_refused(string scope)
    {
        var (status, body) = await RegisterAsync(Registration("http://127.0.0.1:41234/callback", scope));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("Allowed Client Scopes", body.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task A_client_that_sends_no_scope_still_gets_buddy_mcp_read_as_its_only_optional_scope()
    {
        var (status, body) = await RegisterAsync(Registration("http://127.0.0.1:41234/callback", scope: null));
        Assert.Equal(HttpStatusCode.Created, status);

        var client = (await fixture.GetKeycloakClientAsync(body.GetProperty("client_id").GetString()!))!.Value;
        Assert.Equal(["buddy-mcp:read"], client.GetProperty("optionalClientScopes").EnumerateArray().Select(scope => scope.GetString()));
    }

    [Theory]
    [InlineData("buddy-mcp:read")]
    [InlineData(null)]
    public async Task A_confidential_client_is_refused_so_no_secret_is_handed_out(string? scope)
    {
        var (status, body) = await RegisterAsync(Registration("http://127.0.0.1:41234/callback", scope, authMethod: "client_secret_basic"));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.False(body.TryGetProperty("client_secret", out _));
    }

    [Fact]
    public async Task The_implicit_and_password_grants_are_stripped_from_a_registration()
    {
        var (implicitStatus, implicitBody) = await RegisterAsync(Registration("http://127.0.0.1:41234/callback", grantTypes: ["implicit"], responseTypes: ["token"]));
        var (passwordStatus, passwordBody) = await RegisterAsync(Registration("http://127.0.0.1:41234/callback", grantTypes: ["password"]));

        foreach (var (status, body) in new[] { (implicitStatus, implicitBody), (passwordStatus, passwordBody) })
        {
            Assert.Equal(HttpStatusCode.Created, status);
            var client = (await fixture.GetKeycloakClientAsync(body.GetProperty("client_id").GetString()!))!.Value;
            Assert.False(client.GetProperty("implicitFlowEnabled").GetBoolean());
            Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        }
    }

    // The whole point of the scope's mappers: a registered client's token is for the MCP server (and not the
    // REST API), carries buddy-mcp:read, and has the groups the membership middleware needs.
    [Fact]
    public async Task A_registered_client_gets_a_token_for_the_mcp_audience_with_the_read_scope_and_the_groups_after_the_guardian_consents()
    {
        const string redirectUri = "http://127.0.0.1:41234/callback";
        var guardian = await fixture.CreateUserAsync();
        var clientId = await fixture.RegisterClientAsync(redirectUri);

        var code = await fixture.GrantConsentAsync(guardian, clientId, redirectUri);
        var claims = Payload(await fixture.ExchangeCodeAsync(clientId, redirectUri, code));

        var audiences = claims.GetProperty("aud").ValueKind == JsonValueKind.Array
            ? claims.GetProperty("aud").EnumerateArray().Select(audience => audience.GetString()).ToArray()
            : [claims.GetProperty("aud").GetString()];
        Assert.Contains("buddy-mcp", audiences);
        Assert.DoesNotContain("buddy-api", audiences);
        Assert.Contains("buddy-mcp:read", claims.GetProperty("scope").GetString()!.Split(' '));
        Assert.Contains("buddy-guardian", claims.GetProperty("groups").EnumerateArray().Select(group => group.GetString()));
    }

    private static JsonElement Payload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        return JsonElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
    }
}
