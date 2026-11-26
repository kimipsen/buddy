using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

using Alba;

using buddy.Features.FlashCards;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Common.Concurrency;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Testcontainers.PostgreSql;

using Xunit;

namespace buddy.IntegrationTests.Fixtures;

// Shared across the whole test run (see BuddyApiCollection) -- starting Postgres, Keycloak and
// mailpit per test class would dominate wall-clock time. Every aggregate in this codebase is
// keyed by a fresh Guid, so tests don't need the database reset between runs; they just need to
// avoid touching the fixed set of seeded Keycloak users' own profiles from more than one place.
public sealed class BuddyApiFixture : IAsyncLifetime
{
    // The same Keycloak the app runs on (.devcontainer/docker-compose.yml, deploy/azure/keycloak/Dockerfile);
    // KeycloakParityTests fails when they drift apart.
    public const string KeycloakImage = "quay.io/keycloak/keycloak:26.8.2";

    private const string RealmName = "buddy-test";
    private const string Audience = "buddy-api";
    private const string AdminClientId = "buddy-admin-cli";
    private const string AdminClientSecret = "buddy-admin-cli-test-secret";

    private PostgreSqlContainer _postgres = null!;
    private IContainer _keycloak = null!;
    private IContainer _mailpit = null!;
    private HttpClient _mailpitClient = null!;
    private readonly Dictionary<string, string> _tokenCache = [];
    private readonly Dictionary<string, Task<IAlbaHost>> _sharedHosts = [];
    private Dictionary<string, string?> _configOverrides = [];

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18")
            .Build();

        var realmImportPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestRealm.json");

        _keycloak = new ContainerBuilder(KeycloakImage)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
            .WithCommand("start-dev", "--import-realm")
            // Without a cap the JVM sizes its heap from the whole machine's RAM and grew past 2.5 GB
            // in a full run, next to the devcontainer's own Keycloak. The container limit is a
            // backstop for the JVM's non-heap memory.
            .WithEnvironment("JAVA_OPTS_KC_HEAP", "-Xms64m -Xmx512m")
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new Docker.DotNet.Models.HostConfig();
                parameters.HostConfig.Memory = 1024L * 1024 * 1024;
            })
            // WithResourceMapping treats the target as a directory and keeps the source file's own
            // name (TestRealm.json) -- it does not rename to a target basename. Point it at the
            // import directory itself; Keycloak's dir importer picks up any *.json file inside.
            .WithResourceMapping(FilePath.Of(realmImportPath), DirectoryPath.Of("/opt/keycloak/data/import"))
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

        var keycloakBaseUrl = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}";
        var keycloakAuthority = $"{keycloakBaseUrl}/realms/{RealmName}";
        var mailpitHttpPort = _mailpit.GetMappedPublicPort(8025);

        _mailpitClient = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{mailpitHttpPort}") };

        var adminClientSecret = await SetUpKeycloakAdminServiceAccountAsync(keycloakBaseUrl);

        _configOverrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["Authentication:Keycloak:Authority"] = keycloakAuthority,
            ["Authentication:Keycloak:Audience"] = Audience,
            ["Authentication:Keycloak:RequireHttpsMetadata"] = "false",
            // On for the whole suite, so every existing test runs through the MCP-aware pipeline; the
            // flag-off tests build their own host (see RestAuthenticationRegressionTests).
            ["Features:Mcp"] = "true",
            ["Authentication:KeycloakAdmin:Realm"] = RealmName,
            ["Authentication:KeycloakAdmin:TokenEndpoint"] = $"{keycloakBaseUrl}/realms/master/protocol/openid-connect/token",
            ["Authentication:KeycloakAdmin:AdminBaseUrl"] = $"{keycloakBaseUrl}/admin/realms/{RealmName}",
            ["Authentication:KeycloakAdmin:ClientId"] = AdminClientId,
            ["Authentication:KeycloakAdmin:ClientSecret"] = adminClientSecret,
            ["Mail:Host"] = _mailpit.Hostname,
            ["Mail:Port"] = _mailpit.GetMappedPublicPort(1025).ToString(CultureInfo.InvariantCulture),

            // The whole suite shares this host, and under TestServer every anonymous request has the
            // same (null) client IP -- production limits would throttle unrelated tests. Rate limits
            // themselves are tested on a separate low-limit host (RateLimitingTests, CreateHostAsync).
            ["RateLimiting:Authenticated:TokenLimit"] = "1000000",
            ["RateLimiting:Authenticated:TokensPerPeriod"] = "1000000",
            ["RateLimiting:Anonymous:TokenLimit"] = "1000000",
            ["RateLimiting:Anonymous:TokensPerPeriod"] = "1000000",
            ["RateLimiting:IcalFeed:TokenLimit"] = "1000000",
            ["RateLimiting:IcalFeed:TokensPerPeriod"] = "1000000",
            ["RateLimiting:AiAssistant:PermitLimit"] = "1000000",
            ["RateLimiting:OutboundEmail:PermitLimit"] = "1000000",
            ["RateLimiting:PersonalDataExport:PermitLimit"] = "1000000",
            ["RateLimiting:VisitSummaryShare:PermitLimit"] = "1000000",
            ["RateLimiting:Mealie:PermitLimit"] = "1000000",
            ["RateLimiting:MealieImages:PermitLimit"] = "1000000",
            ["RateLimiting:Mcp:PermitLimit"] = "1000000",

            // The client cleanup is a daily job that deletes clients in the shared Keycloak: no test host runs
            // it (the cleanup tests build McpClientCleaner themselves, and McpClientCleanupTests asserts this).
            ["Mcp:ClientCleanup:Enabled"] = "false"
        };

        Host = await AlbaHost.For<global::Program>(x =>
        {
            x.ConfigureServices(services =>
            {
                // Inert unless a test arms it -- see ConcurrentWriterUserEventStore.
                services.AddSingleton<MartenUserEventStore>();
                services.Replace(ServiceDescriptor.Singleton<IUserEventStore, ConcurrentWriterUserEventStore>());

                // Same for the progress store -- see ConcurrentWriterProgressEventStore.
                services.AddSingleton<MartenProgressEventStore>();
                services.Replace(ServiceDescriptor.Singleton<IProgressEventStore, ConcurrentWriterProgressEventStore>());

                // And the flash-card practice store -- see TestFlashCardPracticeEventStore.
                services.AddSingleton<MartenFlashCardPracticeEventStore>();
                services.AddSingleton<TestFlashCardPracticeEventStore>();
                services.Replace(ServiceDescriptor.Singleton<IFlashCardPracticeEventStore>(sp => sp.GetRequiredService<TestFlashCardPracticeEventStore>()));
            });
        })
            .WithConfiguration(_configOverrides);
    }

    // A second API host on the same Postgres/Keycloak/Mailpit, with extra configuration layered on
    // top of the shared host's -- for tests that need settings the shared host can't have (low rate
    // limits, a broken dependency) or extra test services (an in-memory telemetry exporter). The
    // caller owns and disposes it.
    public async Task<IAlbaHost> CreateHostAsync(
        IReadOnlyDictionary<string, string?> overrides,
        Action<IServiceCollection>? configureServices = null)
    {
        var merged = new Dictionary<string, string?>(_configOverrides);

        foreach (var (key, value) in overrides)
        {
            merged[key] = value;
        }

        return await AlbaHost.For<global::Program>(x =>
        {
            x.ConfigureServices(services => configureServices?.Invoke(services));
        })
            .WithConfiguration(merged);
    }

    // A CreateHostAsync host started once per key and shared by every test that asks for it; the
    // fixture disposes it at the end of the run. Prefer this over a host per test: each host compiles
    // its own Wolverine handlers and Marten stores at runtime, and that generated code stays in
    // memory after the host is disposed, so a host per test grew the test process past 7 GB. The
    // whole suite is one serial collection, so a test that captures something through
    // configureServices (logs, spans, a clock) can keep it in a static field and reset it in
    // InitializeAsync.
    public Task<IAlbaHost> SharedHostAsync(
        string key,
        IReadOnlyDictionary<string, string?> overrides,
        Action<IServiceCollection>? configureServices = null)
    {
        lock (_sharedHosts)
        {
            if (!_sharedHosts.TryGetValue(key, out var host))
            {
                host = CreateHostAsync(overrides, configureServices);
                _sharedHosts[key] = host;
            }

            return host;
        }
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

    // A token for the same user from another test-realm client, uncached. "buddy-other" has no
    // buddy-api audience mapper, so the REST API must reject it (the wrong-audience case).
    public async Task<string> GetAccessTokenForClientAsync(string clientId, TestUser user, string scope = "openid profile email")
    {
        using var client = new HttpClient();
        var tokenEndpoint = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}/realms/{RealmName}/protocol/openid-connect/token";

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = user.Username,
            ["password"] = user.Password,
            ["scope"] = scope
        }));

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Keycloak refused a '{clientId}' token for '{user.Username}' (scope '{scope}'): {await response.Content.ReadAsStringAsync()}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException($"Keycloak token response for '{user.Username}' on '{clientId}' had no access_token.");
    }

    // A buddy-mcp token for the user. With the default scope it carries buddy-mcp:read; pass "openid"
    // for one with the right audience (the client's own mapper) but without the scope.
    public Task<string> GetMcpAccessTokenAsync(TestUser user, string scope = "openid profile email buddy-mcp:read") =>
        GetAccessTokenForClientAsync("buddy-mcp", user, scope);

    public Task<string> GetAccessTokenAsync(TestUser user) => GetAccessTokenAsync(user.Username, user.Password);

    // Tries a direct-grant login without caching and returns Keycloak's error_description, or null
    // when it succeeds. Tells a wrong password ("Invalid user credentials") apart from a right one
    // with a pending required action ("Account is not fully set up"), which a one-time password has.
    public async Task<string?> GetPasswordGrantErrorAsync(string username, string password)
    {
        using var client = new HttpClient();
        var tokenEndpoint = $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}/realms/{RealmName}/protocol/openid-connect/token";

        using var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = Audience,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid"
        }));

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.TryGetProperty("error_description", out var description) ? description.GetString() : payload.ToString();
    }

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
    // The user is in the buddy-guardian Keycloak group unless `groups` says otherwise; pass [] for an
    // account BuddyMemberMiddleware turns away.
    public async Task<TestUser> CreateUserAsync(string? givenName = null, string? familyName = null, bool emailVerified = true, string[]? groups = null, CancellationToken cancellationToken = default)
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
            emailVerified,
            enabled = true,
            firstName = givenName ?? "Test",
            lastName = familyName ?? suffix,
            groups = (groups ?? [KeycloakGroups.Guardian]).Select(group => $"/{group}").ToArray(),
            credentials = new[] { new { type = "password", value = password, temporary = false } }
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        return new TestUser(username, password, email);
    }

    // A child provisioned via POST /users/me/children gets a temporary password with a pending
    // UPDATE_PASSWORD required action, which blocks direct-grant login until it's completed -- not
    // something the direct-grant flow itself can satisfy. This mirrors what the child's own device
    // would do on first login (set a permanent password), using the master-realm admin-cli
    // already used elsewhere in this fixture, so tests can then log in as the child normally.
    public async Task SetPermanentPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        var userId = users.EnumerateArray().First().GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"Keycloak has no user named '{username}' in realm '{RealmName}'.");

        var response = await client.PutAsJsonAsync($"/admin/realms/{RealmName}/users/{userId}/reset-password", new
        {
            type = "password",
            value = newPassword,
            temporary = false
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    // Verifies role assignment actually happened (KeycloakAdminClient.AssignChildRoleAsync) by
    // reading the user's realm role mappings back from the Admin API, rather than just trusting
    // that provisioning didn't throw.
    public async Task<string[]> GetAssignedRealmRoleNamesAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        var userId = users.EnumerateArray().First().GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"Keycloak has no user named '{username}' in realm '{RealmName}'.");

        var roles = await client.GetFromJsonAsync<JsonElement[]>($"/admin/realms/{RealmName}/users/{userId}/role-mappings/realm", cancellationToken) ?? [];
        return [.. roles.Select(role => role.GetProperty("name").GetString()!)];
    }

    // The names of the Keycloak groups the user is in, read back from the Admin API.
    public async Task<string[]> GetGroupNamesAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        var userId = users.EnumerateArray().First().GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"Keycloak has no user named '{username}' in realm '{RealmName}'.");

        var groups = await client.GetFromJsonAsync<JsonElement[]>($"/admin/realms/{RealmName}/users/{userId}/groups", cancellationToken) ?? [];
        return [.. groups.Select(group => group.GetProperty("name").GetString()!)];
    }

    // Whether Keycloak still has an account with this username (account deletion removes it).
    public async Task<bool> KeycloakUserExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        return users.GetArrayLength() > 0;
    }

    // The test Keycloak's base URL (no realm), for tests that talk to it directly.
    public string KeycloakBaseUrl => $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}";

    // Anonymous dynamic client registration (RFC 7591), exactly as an MCP client does it, with the
    // body as given: returns the response so a test can look at a refusal's status and error.
    public async Task<HttpResponseMessage> RegisterClientRawAsync(object body, string? realm = null, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        return await client.PostAsJsonAsync($"{KeycloakBaseUrl}/realms/{realm ?? RealmName}/clients-registrations/openid-connect", body, cancellationToken);
    }

    // A throwaway realm from a realm representation, for a test that needs settings the shared realm can't have
    // (a small max-clients cap). Uses the master realm's admin account. The caller deletes it.
    public async Task CreateRealmAsync(string realmJson, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync(cancellationToken));

        using var response = await client.PostAsync("/admin/realms", new StringContent(realmJson, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Keycloak refused to create the realm ({(int)response.StatusCode}); the master admin account must be able to create realms: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }

    public async Task DeleteRealmAsync(string realm, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync(cancellationToken));

        using var response = await client.DeleteAsync($"/admin/realms/{realm}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Registers a public PKCE client like Claude Code or VS Code do (loopback redirect, buddy-mcp:read)
    // and returns the client_id Keycloak assigned (a UUID).
    public async Task<string> RegisterClientAsync(string redirectUri = "http://127.0.0.1:33333/callback", CancellationToken cancellationToken = default)
    {
        using var response = await RegisterClientRawAsync(new
        {
            client_name = $"Test client {Guid.NewGuid():N}"[..24],
            redirect_uris = new[] { redirectUri },
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            scope = "buddy-mcp:read"
        }, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Keycloak refused the client registration: {response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return payload.GetProperty("client_id").GetString()
            ?? throw new InvalidOperationException("Keycloak's registration response had no client_id.");
    }

    // The client's representation from the Admin API, or null when no such client exists.
    public async Task<JsonElement?> GetKeycloakClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var clients = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/clients?clientId={Uri.EscapeDataString(clientId)}", cancellationToken);
        return clients.GetArrayLength() == 0 ? null : clients[0].Clone();
    }

    public async Task<bool> KeycloakClientExistsAsync(string clientId, CancellationToken cancellationToken = default) =>
        await GetKeycloakClientAsync(clientId, cancellationToken) is not null;

    // Deletes the Keycloak account behind a test user; its consents go with it.
    public async Task DeleteKeycloakUserAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        var id = users[0].GetProperty("id").GetString();

        using var response = await client.DeleteAsync($"/admin/realms/{RealmName}/users/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Walks Keycloak's authorization-code flow as the guardian's browser would: the login form, then the
    // consent screen, and accepts it. Keycloak stores the consent when the screen is accepted. The redirect
    // back to the client is not followed; its code is returned. Keycloak has no Admin API call that
    // creates a consent, so this is the only way a test gets one.
    // Cookies are kept by hand: Keycloak marks its login cookies Secure, and a CookieContainer won't send
    // those over the plain HTTP of the test container.
    public async Task<string> GrantConsentAsync(TestUser user, string clientId, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new HttpClient(handler);
        var cookies = new Dictionary<string, string>();

        async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
            }

            var response = await http.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    var pair = setCookie.Split(';')[0];
                    var separator = pair.IndexOf('=', StringComparison.Ordinal);
                    var (name, value) = (pair[..separator], pair[(separator + 1)..]);
                    if (value.Length == 0)
                    {
                        cookies.Remove(name);
                    }
                    else
                    {
                        cookies[name] = value;
                    }
                }
            }

            return response;
        }

        var authUrl = $"{KeycloakBaseUrl}/realms/{RealmName}/protocol/openid-connect/auth" +
            $"?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code" +
            "&scope=openid%20buddy-mcp%3Aread&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256";

        var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, authUrl));
        for (var step = 0; step < 10; step++)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location!);
                if (location.ToString().StartsWith(redirectUri, StringComparison.Ordinal))
                {
                    return System.Web.HttpUtility.ParseQueryString(location.Query)["code"]
                        ?? throw new InvalidOperationException($"The redirect back to '{clientId}' carried no code: {location}");
                }

                response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, location));
                continue;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var fields = html.Contains("name=\"username\"", StringComparison.Ordinal)
                ? new Dictionary<string, string> { ["username"] = user.Username, ["password"] = user.Password }
                : html.Contains("name=\"accept\"", StringComparison.Ordinal)
                    ? new Dictionary<string, string> { ["accept"] = "Yes" }
                    : throw new InvalidOperationException($"Neither a login nor a consent form in Keycloak's page ({(int)response.StatusCode}): {html}");

            response = await SendAsync(FirstFormRequest(response.RequestMessage!.RequestUri!, html, fields));
        }

        throw new InvalidOperationException($"The authorization flow for '{clientId}' did not reach the redirect URI in 10 steps.");
    }

    // The RFC 7636 appendix B verifier whose S256 challenge GrantConsentAsync sends.
    public const string CodeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    // Redeems the code from GrantConsentAsync as a public PKCE client does and returns the access token.
    public async Task<string> ExchangeCodeAsync(string clientId, string redirectUri, string code, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        using var response = await http.PostAsync($"{KeycloakBaseUrl}/realms/{RealmName}/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code"] = code,
            ["code_verifier"] = CodeVerifier
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Keycloak refused the code exchange for '{clientId}': {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return payload.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak's token response had no access_token.");
    }

    // The POST for the page's first <form>: its action, its hidden inputs and the given fields.
    private static HttpRequestMessage FirstFormRequest(Uri pageUri, string html, Dictionary<string, string> fields)
    {
        var form = System.Text.RegularExpressions.Regex.Match(html, "<form[^>]*action=\"(?<action>[^\"]+)\"[^>]*>(?<body>.*?)</form>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!form.Success)
        {
            throw new InvalidOperationException($"No <form> in Keycloak's page: {html}");
        }

        var values = new Dictionary<string, string>();
        foreach (System.Text.RegularExpressions.Match input in System.Text.RegularExpressions.Regex.Matches(form.Groups["body"].Value, "<input[^>]*type=\"hidden\"[^>]*>"))
        {
            var name = System.Text.RegularExpressions.Regex.Match(input.Value, "name=\"([^\"]+)\"");
            var value = System.Text.RegularExpressions.Regex.Match(input.Value, "value=\"([^\"]*)\"");
            if (name.Success)
            {
                values[name.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(value.Success ? value.Groups[1].Value : "");
            }
        }

        foreach (var (key, value) in fields)
        {
            values[key] = value;
        }

        return new HttpRequestMessage(HttpMethod.Post, new Uri(pageUri, System.Net.WebUtility.HtmlDecode(form.Groups["action"].Value)))
        {
            Content = new FormUrlEncodedContent(values)
        };
    }

    // Whether Keycloak marks this account's email as verified (VerifyEmail syncs it).
    public async Task<bool> IsKeycloakEmailVerifiedAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/{RealmName}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        return users.EnumerateArray().First().GetProperty("emailVerified").GetBoolean();
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

    // Production's IKeycloakAdminClient authenticates as a confidential client's service account
    // via client-credentials, then calls the buddy realm's Admin API -- nothing like that exists in
    // the built-in master/admin-cli setup GetAdminTokenAsync uses, so this creates the equivalent
    // for tests: a new client in the master realm (Keycloak's own convention for cross-realm admin
    // access), granted the master realm's auto-generated "{realm}-realm" client's manage-users role
    // -- the same composite Keycloak creates for every realm to let a master-realm principal manage
    // just that realm, without needing global superadmin rights.
    private async Task<string> SetUpKeycloakAdminServiceAccountAsync(string keycloakBaseUrl)
    {
        var adminToken = await GetAdminTokenAsync(CancellationToken.None);

        using var client = new HttpClient { BaseAddress = new Uri(keycloakBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await client.PostAsJsonAsync("/admin/realms/master/clients", new
        {
            clientId = AdminClientId,
            enabled = true,
            publicClient = false,
            serviceAccountsEnabled = true,
            protocol = "openid-connect",
            secret = AdminClientSecret
        });
        createResponse.EnsureSuccessStatusCode();

        var clientDbId = createResponse.Headers.Location?.Segments[^1]
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created admin client.");

        var serviceAccountUser = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/master/clients/{clientDbId}/service-account-user");
        var serviceAccountUserId = serviceAccountUser.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Keycloak did not return an id for the admin client's service account user.");

        var realmManagementClients = await client.GetFromJsonAsync<JsonElement>($"/admin/realms/master/clients?clientId={RealmName}-realm");
        var realmManagementClientDbId = realmManagementClients.EnumerateArray().First().GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"Keycloak has no '{RealmName}-realm' client in the master realm.");

        // manage-users for the guardians' child accounts, manage-clients for the MCP client cleanup
        // (McpClientCleanupService lists, stamps and deletes the dynamically registered clients).
        var roles = new List<JsonElement>();
        foreach (var roleName in new[] { "manage-users", "manage-clients" })
        {
            roles.Add(await client.GetFromJsonAsync<JsonElement>($"/admin/realms/master/clients/{realmManagementClientDbId}/roles/{roleName}"));
        }

        var assignResponse = await client.PostAsJsonAsync(
            $"/admin/realms/master/users/{serviceAccountUserId}/role-mappings/clients/{realmManagementClientDbId}",
            roles);
        assignResponse.EnsureSuccessStatusCode();

        // Since 26.8.2 the Admin API only accepts tokens whose audience is the admin endpoint, which
        // the built-in "kc-admin-api" client scope adds. Keycloak gives it to admin-cli only, so a
        // service account without it gets 401 on every Admin API call (production's
        // buddy-admin-cli needs the same scope, see deploy/README.md).
        var clientScopes = await client.GetFromJsonAsync<JsonElement>("/admin/realms/master/client-scopes");
        var adminApiScopeId = clientScopes.EnumerateArray()
            .Where(scope => scope.GetProperty("name").GetString() == "kc-admin-api")
            .Select(scope => scope.GetProperty("id").GetString())
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Keycloak has no 'kc-admin-api' client scope in the master realm.");

        var scopeResponse = await client.PutAsync($"/admin/realms/master/clients/{clientDbId}/default-client-scopes/{adminApiScopeId}", content: null);
        scopeResponse.EnsureSuccessStatusCode();

        return AdminClientSecret;
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

        foreach (var host in _sharedHosts.Values.Where(host => host.IsCompletedSuccessfully))
        {
            await host.Result.DisposeAsync();
        }

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
