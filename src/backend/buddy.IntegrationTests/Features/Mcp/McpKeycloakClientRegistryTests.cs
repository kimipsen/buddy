using System.Text.Json;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// KeycloakClientRegistry against the real Keycloak container: what the in-memory fake in McpClientCleanerTests
// can't prove, the Admin API contract the cleanup relies on.
[Collection(BuddyApiCollection.Name)]
public sealed class McpKeycloakClientRegistryTests(BuddyApiFixture fixture)
{
    private const string RedirectUri = "http://127.0.0.1:41234/callback";

    private IKeycloakClientRegistry Registry => fixture.Host.Services.GetRequiredService<IKeycloakClientRegistry>();

    [Fact]
    public async Task A_registered_client_is_listed_with_its_scopes_and_as_public()
    {
        var clientId = await fixture.RegisterClientAsync(RedirectUri);

        var listed = (await Registry.ListClientsAsync(CancellationToken.None)).Single(client => client.ClientId == clientId);

        Assert.True(listed.PublicClient);
        Assert.Contains("buddy-mcp:read", listed.Scopes);
        Assert.Null(listed.FirstSeen);
        Assert.Equal(clientId, listed.Id);
        Assert.Contains(await Registry.ListClientsAsync(CancellationToken.None), client => client.ClientId == "buddy-mcp");
    }

    // The age rule depends on buddy.firstSeen surviving a real round trip, and on the stamp leaving the rest of the
    // client alone: a client update replaces the attributes map, so only read, merge, write is safe.
    [Fact]
    public async Task The_stamp_is_read_back_on_list_and_changes_nothing_else_about_the_client()
    {
        var clientId = await fixture.RegisterClientAsync(RedirectUri);
        var before = (await fixture.GetKeycloakClientAsync(clientId))!.Value;
        var id = before.GetProperty("id").GetString()!;

        await Registry.SetClientAttributeAsync(id, KeycloakClientRegistry.FirstSeenAttribute, "2026-03-01T09:00:00.0000000+00:00", CancellationToken.None);

        var after = (await fixture.GetKeycloakClientAsync(clientId))!.Value;
        var listed = (await Registry.ListClientsAsync(CancellationToken.None)).Single(client => client.ClientId == clientId);
        Assert.Equal("2026-03-01T09:00:00.0000000+00:00", listed.FirstSeen);

        var attributesBefore = before.GetProperty("attributes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
        var attributesAfter = after.GetProperty("attributes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
        Assert.Contains(attributesBefore, pair => pair.Key == "pkce.code.challenge.method");
        Assert.Equal(attributesBefore.Count + 1, attributesAfter.Count);
        Assert.All(attributesBefore, pair => Assert.Equal(pair.Value, attributesAfter[pair.Key]));

        foreach (var property in new[] { "redirectUris", "defaultClientScopes", "optionalClientScopes", "webOrigins" })
        {
            Assert.Equal(before.GetProperty(property).GetRawText(), after.GetProperty(property).GetRawText());
        }

        foreach (var property in new[] { "publicClient", "consentRequired", "fullScopeAllowed", "standardFlowEnabled", "implicitFlowEnabled", "directAccessGrantsEnabled", "enabled" })
        {
            Assert.Equal(before.GetProperty(property).GetBoolean(), after.GetProperty(property).GetBoolean());
        }
    }

    [Fact]
    public async Task Deleting_a_client_removes_it_and_a_second_delete_is_not_an_error()
    {
        var clientId = await fixture.RegisterClientAsync(RedirectUri);
        var id = (await fixture.GetKeycloakClientAsync(clientId))!.Value.GetProperty("id").GetString()!;

        await Registry.DeleteClientAsync(id, CancellationToken.None);
        await Registry.DeleteClientAsync(id, CancellationToken.None);

        Assert.False(await fixture.KeycloakClientExistsAsync(clientId));
    }

    [Fact]
    public async Task Setting_an_attribute_on_a_client_that_is_gone_does_nothing()
    {
        await Registry.SetClientAttributeAsync(Guid.NewGuid().ToString(), KeycloakClientRegistry.FirstSeenAttribute, "x", CancellationToken.None);
    }

    [Fact]
    public async Task The_paged_user_list_adds_up_to_the_count_and_consents_follow_the_user()
    {
        var guardian = await fixture.CreateUserAsync();
        var other = await fixture.CreateUserAsync();
        var clientId = await fixture.RegisterClientAsync(RedirectUri);
        await fixture.GrantConsentAsync(guardian, clientId, RedirectUri);

        var users = await Registry.ListUserIdsAsync(CancellationToken.None);
        Assert.Equal(await Registry.CountUsersAsync(CancellationToken.None), users.Count);
        Assert.Equal(users.Count, users.Distinct().Count());

        var guardianId = await UserIdAsync(guardian.Username);
        Assert.Contains(guardianId, users);
        Assert.Contains(clientId, await Registry.ListConsentedClientIdsAsync(guardianId, CancellationToken.None));
        Assert.Empty(await Registry.ListConsentedClientIdsAsync(await UserIdAsync(other.Username), CancellationToken.None));
        Assert.Empty(await Registry.ListConsentedClientIdsAsync(Guid.NewGuid().ToString(), CancellationToken.None));
    }

    private async Task<string> UserIdAsync(string username)
    {
        using var http = new HttpClient();
        using var token = await http.PostAsync($"{fixture.KeycloakBaseUrl}/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = "admin"
        }));
        var access = (await token.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{fixture.KeycloakBaseUrl}/admin/realms/buddy-test/users?username={username}&exact=true");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        using var response = await http.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetString()!;
    }
}
