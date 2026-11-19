using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The integration tests must run the Keycloak the app runs on, configured like the app's realm.
// They once stayed on 21.1.1 while the devcontainer and the Azure image moved to 26.x, so 26.x-only
// behaviour (the "basic" scope carrying `sub`, the declarative user profile's VERIFY_PROFILE)
// passed here and broke e2e on a fresh realm. No containers needed: everything is read from the
// checked-in files.
public sealed partial class KeycloakParityTests
{
    private const string UserProfileProvider = "org.keycloak.userprofile.UserProfileProvider";

    [Theory]
    [InlineData(".devcontainer/docker-compose.yml")]
    [InlineData("deploy/azure/keycloak/Dockerfile")]
    public void The_fixture_runs_the_same_Keycloak_image_as(string path)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), path));

        var images = KeycloakImageReference().Matches(text).Select(match => match.Groups["image"].Value).Distinct().ToArray();

        Assert.True(images.Length > 0, $"No quay.io/keycloak/keycloak image reference in {path}.");
        Assert.All(images, image => Assert.Equal(BuddyApiFixture.KeycloakImage, image));
    }

    // Which attributes are required decides whether Keycloak lets a user sign in (a child has no
    // email), so the test realm must enforce exactly what the app realm does.
    [Fact]
    public void The_test_realm_has_the_same_user_profile_as_the_app_realm()
    {
        var appProfile = UserProfile(".devcontainer/keycloak/buddy-realm.json");
        var testProfile = UserProfile("src/backend/buddy.IntegrationTests/Fixtures/TestRealm.json");

        Assert.True(
            JsonNode.DeepEquals(appProfile, testProfile),
            $"TestRealm.json's user profile differs from buddy-realm.json's. Copy the app realm's {UserProfileProvider} component.\n" +
            $"App:  {appProfile?.ToJsonString()}\nTest: {testProfile?.ToJsonString()}");
    }

    private const string AppRealm = ".devcontainer/keycloak/buddy-realm.json";
    private const string TestRealm = "src/backend/buddy.IntegrationTests/Fixtures/TestRealm.json";
    private const string RegistrationPolicy = "org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy";

    // Keycloak creates these in every realm; its max-clients policy counts them (docs/backend/analysis/mcp-server.md).
    private const int BuiltInClients = 6;
    private const int DynamicRegistrationCap = 50;

    // A realm import that lists clientScopes replaces Keycloak's own defaults, so both files carry the full
    // set plus buddy-mcp:read. They must stay the same set, or a token from the tests would differ from one
    // the app realm issues.
    [Fact]
    public void The_test_realm_has_the_same_client_scopes_as_the_app_realm()
    {
        var app = Realm(AppRealm)["clientScopes"];
        var test = Realm(TestRealm)["clientScopes"];

        Assert.True(JsonNode.DeepEquals(app, test), "TestRealm.json's clientScopes differ from buddy-realm.json's. Keep the two lists identical.");
        Assert.True(JsonNode.DeepEquals(Realm(AppRealm)["defaultDefaultClientScopes"], Realm(TestRealm)["defaultDefaultClientScopes"]));
        Assert.True(JsonNode.DeepEquals(Realm(AppRealm)["defaultOptionalClientScopes"], Realm(TestRealm)["defaultOptionalClientScopes"]));
    }

    [Theory]
    [InlineData(AppRealm)]
    [InlineData(TestRealm)]
    public void The_buddy_mcp_read_scope_carries_the_audience_and_the_groups_and_shows_on_the_consent_screen(string realmPath)
    {
        var scope = ClientScope(realmPath, "buddy-mcp:read");

        Assert.Equal("true", scope["attributes"]!["display.on.consent.screen"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(scope["attributes"]!["consent.screen.text"]?.GetValue<string>()));

        var mappers = scope["protocolMappers"]!.AsArray();
        var audience = Assert.Single(mappers, mapper => mapper!["protocolMapper"]!.GetValue<string>() == "oidc-audience-mapper");
        Assert.Equal("buddy-mcp", audience!["config"]!["included.client.audience"]!.GetValue<string>());
        Assert.Equal("true", audience["config"]!["access.token.claim"]!.GetValue<string>());

        // Without it every MCP caller would fail UseBuddyMembers (KeycloakGroups).
        var groups = Assert.Single(mappers, mapper => mapper!["protocolMapper"]!.GetValue<string>() == "oidc-group-membership-mapper");
        Assert.Equal("groups", groups!["config"]!["claim.name"]!.GetValue<string>());
        Assert.Equal("true", groups["config"]!["access.token.claim"]!.GetValue<string>());
    }

    // Only the MCP scope is a realm default for new clients: a client that registers without a scope can
    // still ask for it, and none is handed offline_access, address, phone or microprofile-jwt.
    [Theory]
    [InlineData(AppRealm)]
    [InlineData(TestRealm)]
    public void New_clients_get_buddy_mcp_read_as_their_only_optional_scope(string realmPath) =>
        Assert.Equal(["buddy-mcp:read"], Realm(realmPath)["defaultOptionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()));

    [Theory]
    [InlineData(AppRealm)]
    [InlineData(TestRealm)]
    public void The_buddy_mcp_client_is_public_and_asks_for_buddy_mcp_read_only_as_an_optional_scope(string realmPath)
    {
        var client = Client(realmPath, "buddy-mcp");

        Assert.True(client["publicClient"]!.GetValue<bool>());
        Assert.False(client["serviceAccountsEnabled"]!.GetValue<bool>());
        Assert.False(client["implicitFlowEnabled"]!.GetValue<bool>());
        Assert.Equal(["buddy-mcp:read"], client["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()));
        Assert.DoesNotContain(client["defaultClientScopes"]!.AsArray(), scope => scope!.GetValue<string>() == "buddy-mcp:read");

        // The client's own audience mapper: a token minted without the optional scope still has the audience.
        Assert.Contains(client["protocolMappers"]!.AsArray(), mapper => mapper!["name"]!.GetValue<string>() == "audience-buddy-mcp");
    }

    // The app realm's buddy-mcp client is the one a person types a client id for: authorization code with
    // PKCE S256, consent shown, redirect URIs only on loopback.
    [Fact]
    public void The_app_realms_buddy_mcp_client_requires_PKCE_and_consent_and_redirects_only_to_loopback()
    {
        var client = Client(AppRealm, "buddy-mcp");

        Assert.True(client["standardFlowEnabled"]!.GetValue<bool>());
        Assert.False(client["directAccessGrantsEnabled"]!.GetValue<bool>());
        Assert.True(client["consentRequired"]!.GetValue<bool>());
        Assert.Equal("S256", client["attributes"]!["pkce.code.challenge.method"]!.GetValue<string>());
        Assert.All(client["redirectUris"]!.AsArray(), uri => Assert.Matches(@"^http://(127\.0\.0\.1|\[::1\]|localhost)/\*$", uri!.GetValue<string>()));
    }

    // The test realm's buddy-mcp client exists to mint tokens with a password grant, which the app realm's never allows.
    [Fact]
    public void The_test_realms_buddy_mcp_client_is_the_only_direct_grant_one()
    {
        Assert.True(Client(TestRealm, "buddy-mcp")["directAccessGrantsEnabled"]!.GetValue<bool>());
        Assert.False(Client(AppRealm, "buddy-mcp")["directAccessGrantsEnabled"]!.GetValue<bool>());
    }

    // The anonymous registration policies, the client profiles and the client policies make up what a
    // self-registered client may do, so the tests must run against exactly the app realm's.
    [Fact]
    public void The_test_realm_has_the_same_registration_policies_as_the_app_realm()
    {
        Assert.True(
            JsonNode.DeepEquals(Realm(AppRealm)["components"]![RegistrationPolicy], Realm(TestRealm)["components"]![RegistrationPolicy]),
            "TestRealm.json's client registration policies differ from buddy-realm.json's.");
        Assert.True(JsonNode.DeepEquals(Realm(AppRealm)["clientProfiles"], Realm(TestRealm)["clientProfiles"]));
        Assert.True(JsonNode.DeepEquals(Realm(AppRealm)["clientPolicies"], Realm(TestRealm)["clientPolicies"]));
    }

    // Keycloak's max-clients policy counts every client in the realm, its own built-ins and Buddy's too, so a
    // plain 50 would leave room for about 41 registrations and shrink silently as clients are added. The
    // setting is the clients the realm starts with plus 50.
    [Theory]
    [InlineData(AppRealm)]
    [InlineData(TestRealm)]
    public void The_max_clients_policy_is_the_realms_own_clients_plus_the_dynamic_registration_cap(string realmPath)
    {
        var realm = Realm(realmPath);
        var maxClients = AnonymousPolicy(realm, "max-clients")["config"]!["max-clients"]![0]!.GetValue<string>();

        Assert.Equal((BuiltInClients + realm["clients"]!.AsArray().Count + DynamicRegistrationCap).ToString(System.Globalization.CultureInfo.InvariantCulture), maxClients);
    }

    [Theory]
    [InlineData(AppRealm)]
    [InlineData(TestRealm)]
    public void Anonymous_registrations_are_limited_to_the_mcp_scope_loopback_and_known_callback_hosts(string realmPath)
    {
        var realm = Realm(realmPath);

        var scopes = AnonymousPolicy(realm, "allowed-client-templates")["config"]!;
        Assert.Equal("false", scopes["allow-default-scopes"]![0]!.GetValue<string>());
        var allowed = scopes["allowed-client-scopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ToArray();
        Assert.Contains("buddy-mcp:read", allowed);
        Assert.DoesNotContain("offline_access", allowed);

        var hosts = AnonymousPolicy(realm, "trusted-hosts")["config"]!;
        Assert.Equal(["localhost", "127.0.0.1", "claude.ai", "vscode.dev", "chatgpt.com"], hosts["trusted-hosts"]!.AsArray().Select(host => host!.GetValue<string>()));
        Assert.Equal("true", hosts["client-uris-must-match"]![0]!.GetValue<string>());
        Assert.Equal("false", hosts["host-sending-registration-request-must-match"]![0]!.GetValue<string>());

        // Present with their stock settings: consent screen, no full scope, no client-supplied mappers.
        AnonymousPolicy(realm, "consent-required");
        AnonymousPolicy(realm, "scope");
        AnonymousPolicy(realm, "allowed-protocol-mappers");
    }

    // The service account behind IKeycloakClientRegistry lists, stamps and deletes clients, and reads
    // consents through the users resource.
    [Fact]
    public void The_app_realms_admin_service_account_can_manage_users_and_clients()
    {
        var serviceAccount = Realm(AppRealm)["users"]!.AsArray().Single(user => user!["username"]!.GetValue<string>() == "service-account-buddy-admin-cli");

        Assert.Equal(
            ["manage-clients", "manage-users"],
            serviceAccount!["clientRoles"]!["realm-management"]!.AsArray().Select(role => role!.GetValue<string>()).Order());
    }

    private static JsonNode Realm(string realmPath) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), realmPath)))
        ?? throw new InvalidOperationException($"{realmPath} is empty.");

    private static JsonNode ClientScope(string realmPath, string name) =>
        Realm(realmPath)["clientScopes"]!.AsArray().Single(scope => scope!["name"]!.GetValue<string>() == name)!;

    private static JsonNode Client(string realmPath, string clientId) =>
        Realm(realmPath)["clients"]!.AsArray().Single(client => client!["clientId"]!.GetValue<string>() == clientId)!;

    private static JsonNode AnonymousPolicy(JsonNode realm, string providerId) =>
        realm["components"]![RegistrationPolicy]!.AsArray()
            .Single(policy => policy!["subType"]!.GetValue<string>() == "anonymous" && policy["providerId"]!.GetValue<string>() == providerId)!;

    // The profile is a JSON document stored as a string in the provider component's config.
    private static JsonNode? UserProfile(string realmPath)
    {
        var realm = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), realmPath)));
        var config = realm?["components"]?[UserProfileProvider]?[0]?["config"]?["kc.user.profile.config"]?[0]?.GetValue<string>()
            ?? throw new InvalidOperationException($"{realmPath} has no {UserProfileProvider} component with a kc.user.profile.config.");

        return JsonNode.Parse(config);
    }

    // The tag, without a trailing "@sha256:..." digest pin.
    [GeneratedRegex(@"(?<image>quay\.io/keycloak/keycloak:[^\s@""']+)")]
    private static partial Regex KeycloakImageReference();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "backend")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No repository root (a folder with docs/backend) above {AppContext.BaseDirectory}.");
    }
}
