using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Alba;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Testcontainers.PostgreSql;

using Xunit;

namespace buddy.IntegrationTests.Fixtures;

// Shared across the whole test run (see BuddyApiCollection) -- starting Postgres, Keycloak and
// mailpit per test class would dominate wall-clock time. Every aggregate in this codebase is
// keyed by a fresh Guid, so tests don't need the database reset between runs; they just need to
// avoid touching the fixed set of seeded Keycloak users' own profiles from more than one place.
public sealed class BuddyApiFixture : IAsyncLifetime
{
    private const string RealmName = "buddy-test";
    private const string Audience = "buddy-api";

    private PostgreSqlContainer _postgres = null!;
    private IContainer _keycloak = null!;
    private IContainer _mailpit = null!;
    private HttpClient _mailpitClient = null!;
    private readonly Dictionary<string, string> _tokenCache = [];

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18")
            .Build();

        var realmImportPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestRealm.json");

        _keycloak = new ContainerBuilder("quay.io/keycloak/keycloak:21.1.1")
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithCommand("start-dev", "--import-realm")
            // WithResourceMapping treats the target as a directory and keeps the source file's own
            // name (TestRealm.json) -- it does not rename to a target basename. Point it at the
            // import directory itself; Keycloak's dir importer picks up any *.json file inside.
            .WithResourceMapping(realmImportPath, "/opt/keycloak/data/import")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8080)
                    .ForPath($"/realms/{RealmName}/.well-known/openid-configuration")))
            .Build();

        _mailpit = new ContainerBuilder("axllent/mailpit:latest")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
            .Build();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _keycloak.StartAsync(),
            _mailpit.StartAsync());

        var keycloakAuthority = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}/realms/{RealmName}";
        var mailpitHttpPort = _mailpit.GetMappedPublicPort(8025);

        _mailpitClient = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{mailpitHttpPort}") };

        var configOverrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["Authentication:Keycloak:Authority"] = keycloakAuthority,
            ["Authentication:Keycloak:Audience"] = Audience,
            ["Authentication:Keycloak:RequireHttpsMetadata"] = "false",
            ["Mail:Host"] = _mailpit.Hostname,
            ["Mail:Port"] = _mailpit.GetMappedPublicPort(1025).ToString()
        };

        Host = await AlbaHost.For<global::Program>(_ => { }, ConfigurationOverride.Create(configOverrides));
    }

    // Real access token for one of the seeded test users (see Fixtures/TestRealm.json), obtained
    // from the real Keycloak container via the direct-grant (resource owner password) flow --
    // exercises the actual JwtBearer/JWKS validation and UserIdClaimsTransformation path instead
    // of faking a ClaimsPrincipal in-process.
    public async Task<string> GetAccessTokenAsync(string username, string password)
    {
        if (_tokenCache.TryGetValue(username, out var cached))
        {
            return cached;
        }

        using var client = new HttpClient();
        var tokenEndpoint = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}/realms/{RealmName}/protocol/openid-connect/token";

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = Audience,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid profile email"
        }));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException($"Keycloak token response for '{username}' had no access_token.");

        _tokenCache[username] = token;
        return token;
    }

    public Task<string> GetAccessTokenAsync(TestUser user) => GetAccessTokenAsync(user.Username, user.Password);

    // Convenience for the common case: mint a fresh Keycloak user and materialize its buddy
    // User aggregate (GET /users/me lazily creates it from the token's claims -- see
    // GetOrCreateUserHandler) so command endpoints that require an existing UserId claim
    // (UpdateName, UpdateEmail, CreateCalendar, ...) work against it right away.
    public async Task<(TestUser User, string Token, Guid UserId)> CreateAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await CreateUserAsync(cancellationToken: cancellationToken);
        var token = await GetAccessTokenAsync(user);
        var userId = await GetUserIdAsync(token);

        return (user, token, userId);
    }

    public async Task<Guid> GetUserIdAsync(string token)
    {
        var response = await Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<CurrentUserIdEnvelope>().Id;
    }

    private sealed record CurrentUserIdEnvelope(Guid Id);

    // Creates a disposable, uniquely-named Keycloak user via the Admin REST API (master realm's
    // built-in admin-cli client). This is the primary way tests get an isolated identity: the
    // three named users in TestRealm.json are for simple read-only smoke tests, but anything
    // that mutates a user's own profile, or needs many distinct identities at once (an
    // authorization matrix across owner/contributor/viewer), should mint its own user here
    // rather than compete over a fixed handful of shared accounts.
    public async Task<TestUser> CreateUserAsync(string? givenName = null, string? familyName = null, CancellationToken cancellationToken = default)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var username = $"test-{suffix}";
        var password = $"pw-{suffix}";
        var email = $"{username}@buddy.test";

        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PostAsJsonAsync($"/admin/realms/{RealmName}/users", new
        {
            username,
            email,
            emailVerified = true,
            enabled = true,
            firstName = givenName ?? "Test",
            lastName = familyName ?? suffix,
            credentials = new[] { new { type = "password", value = password, temporary = false } }
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        return new TestUser(username, password, email);
    }

    private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var tokenEndpoint = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}/realms/master/protocol/openid-connect/token";

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = "admin"
        }), cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak admin token response had no access_token.");
    }

    public async Task<JsonElement[]> GetMailpitMessagesToAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        var response = await _mailpitClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/search?query={Uri.EscapeDataString($"to:{emailAddress}")}",
            cancellationToken);

        return [.. response.GetProperty("messages").EnumerateArray()];
    }

    public async Task<string> GetMailpitMessageTextAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var response = await _mailpitClient.GetAsync($"/api/v1/message/{messageId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return payload.GetProperty("Text").GetString() ?? "";
    }

    public async Task DisposeAsync()
    {
        _mailpitClient.Dispose();

        if (Host is not null)
        {
            await Host.DisposeAsync();
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _keycloak.DisposeAsync().AsTask(),
            _mailpit.DisposeAsync().AsTask());
    }
}

public sealed record TestUser(string Username, string Password, string Email);

[CollectionDefinition(Name)]
public sealed class BuddyApiCollection : ICollectionFixture<BuddyApiFixture>
{
    public const string Name = "buddy-api";
}
