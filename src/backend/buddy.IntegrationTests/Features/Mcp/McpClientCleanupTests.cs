using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 4 of docs/backend/analysis/mcp-server.md: McpClientCleaner against the real Keycloak container with a
// fake clock. A pass touches every registered client of the realm, so each test looks only at its own clients;
// the collection runs one test class at a time.
[Collection(BuddyApiCollection.Name)]
public sealed class McpClientCleanupTests(BuddyApiFixture fixture)
{
    private const string RedirectUri = "http://127.0.0.1:41234/callback";

    private McpClientCleaner NewCleaner(FakeTimeProvider clock) =>
        new(fixture.Host.Services.GetRequiredService<IKeycloakClientRegistry>(), clock, Options.Create(new McpClientCleanupOptions { DryRun = false }), NullLogger<McpClientCleaner>.Instance);

    // The fixture turns the job off for every host, so no test host runs it against the shared Keycloak.
    [Fact]
    public void The_shared_host_does_not_run_the_cleanup_job() =>
        Assert.DoesNotContain(fixture.Host.Services.GetServices<IHostedService>(), service => service is McpClientCleanupService);

    private static FakeTimeProvider NewClock() => new(DateTimeOffset.UtcNow);

    [Fact]
    public async Task An_unconsented_client_is_deleted_after_7_days_a_consented_one_stays_and_a_6_day_old_one_is_kept()
    {
        var guardian = await fixture.CreateUserAsync();
        var abandoned = await fixture.RegisterClientAsync(RedirectUri);
        var used = await fixture.RegisterClientAsync(RedirectUri);
        await fixture.GrantConsentAsync(guardian, used, RedirectUri);

        var clock = NewClock();
        var cleaner = NewCleaner(clock);

        // The first pass only starts the clock: Keycloak keeps no creation time, so the cleanup stamps what it sees.
        await cleaner.RunAsync(CancellationToken.None);
        Assert.True(await fixture.KeycloakClientExistsAsync(abandoned));

        clock.Advance(TimeSpan.FromDays(6));
        await cleaner.RunAsync(CancellationToken.None);
        Assert.True(await fixture.KeycloakClientExistsAsync(abandoned), "A 6-day-old registration is inside the grace period.");

        clock.Advance(TimeSpan.FromDays(1));
        await cleaner.RunAsync(CancellationToken.None);
        Assert.False(await fixture.KeycloakClientExistsAsync(abandoned), "7 days without a consent is the end of the grace period.");
        Assert.True(await fixture.KeycloakClientExistsAsync(used), "A client a guardian consented to is never deleted.");
    }

    [Fact]
    public async Task The_first_seen_stamp_is_a_client_attribute_and_a_consented_client_is_never_stamped()
    {
        var guardian = await fixture.CreateUserAsync();
        var abandoned = await fixture.RegisterClientAsync(RedirectUri);
        var used = await fixture.RegisterClientAsync(RedirectUri);
        await fixture.GrantConsentAsync(guardian, used, RedirectUri);

        var clock = NewClock();
        var result = await NewCleaner(clock).RunAsync(CancellationToken.None);

        Assert.True(result.Stamped >= 1);
        Assert.Equal("buddy.firstSeen", KeycloakClientRegistry.FirstSeenAttribute);
        var stamp = (await fixture.GetKeycloakClientAsync(abandoned))!.Value.GetProperty("attributes").GetProperty(KeycloakClientRegistry.FirstSeenAttribute).GetString();
        Assert.Equal(clock.GetUtcNow(), DateTimeOffset.Parse(stamp!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.False((await fixture.GetKeycloakClientAsync(used))!.Value.GetProperty("attributes").TryGetProperty(KeycloakClientRegistry.FirstSeenAttribute, out _));

        // A later pass keeps the first stamp; it never moves the deadline.
        clock.Advance(TimeSpan.FromDays(2));
        await NewCleaner(clock).RunAsync(CancellationToken.None);
        var later = (await fixture.GetKeycloakClientAsync(abandoned))!.Value.GetProperty("attributes").GetProperty(KeycloakClientRegistry.FirstSeenAttribute).GetString();
        Assert.Equal(stamp, later);
    }

    // Keycloak deletes a user's consents with the account, so a client whose only consenting guardian was
    // deleted looks abandoned and goes once it is past the grace period.
    [Fact]
    public async Task A_client_whose_only_consenting_user_was_deleted_is_deleted_after_the_grace_period()
    {
        var guardian = await fixture.CreateUserAsync();
        var client = await fixture.RegisterClientAsync(RedirectUri);
        await fixture.GrantConsentAsync(guardian, client, RedirectUri);

        var clock = NewClock();
        var cleaner = NewCleaner(clock);

        await cleaner.RunAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(30));
        await cleaner.RunAsync(CancellationToken.None);
        Assert.True(await fixture.KeycloakClientExistsAsync(client), "While the guardian exists, the consent keeps the client however old it is.");

        await fixture.DeleteKeycloakUserAsync(guardian.Username);

        // The pass that finds it unconsented starts its grace period, as for any client first seen unconsented.
        await cleaner.RunAsync(CancellationToken.None);
        Assert.True(await fixture.KeycloakClientExistsAsync(client));

        clock.Advance(McpClientCleaner.GracePeriod);
        await cleaner.RunAsync(CancellationToken.None);
        Assert.False(await fixture.KeycloakClientExistsAsync(client));
    }

    [Fact]
    public async Task The_pre_registered_and_built_in_clients_survive_however_old_they_are()
    {
        var clock = NewClock();
        var cleaner = NewCleaner(clock);

        await cleaner.RunAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(400));
        await cleaner.RunAsync(CancellationToken.None);

        // The realm's own clients, none of them with a UUID client_id. The test realm has Buddy's buddy-api,
        // buddy-other and buddy-mcp (buddy-frontend and buddy-admin-cli are the app realm's, KeycloakParityTests).
        foreach (var clientId in new[] { "buddy-mcp", "buddy-api", "buddy-other", "account", "account-console", "admin-cli", "broker", "realm-management", "security-admin-console" })
        {
            Assert.True(await fixture.KeycloakClientExistsAsync(clientId), $"{clientId} must survive the cleanup.");
        }
    }

    // The failsafes beyond the UUID rule: an operator's own client, even one named like a registered client, stays.
    [Fact]
    public async Task A_client_made_by_hand_survives_even_with_a_UUID_client_id_when_it_is_not_public()
    {
        var token = await AdminTokenAsync();
        using var admin = new HttpClient { BaseAddress = new Uri(fixture.KeycloakBaseUrl) };
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var confidentialUuid = Guid.NewGuid().ToString();
        var hand = $"buddy-cursor-{Guid.NewGuid():N}";
        foreach (var (clientId, isPublic) in new[] { (confidentialUuid, false), (hand, true) })
        {
            using var created = await admin.PostAsJsonAsync("/admin/realms/buddy-test/clients", new { clientId, publicClient = isPublic, enabled = true, protocol = "openid-connect" });
            created.EnsureSuccessStatusCode();
        }

        var clock = NewClock();
        var cleaner = NewCleaner(clock);
        await cleaner.RunAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(30));
        await cleaner.RunAsync(CancellationToken.None);

        Assert.True(await fixture.KeycloakClientExistsAsync(confidentialUuid));
        Assert.True(await fixture.KeycloakClientExistsAsync(hand));
    }

    [Fact]
    public async Task A_public_UUID_client_without_a_buddy_mcp_scope_is_not_a_registered_one_and_survives()
    {
        using var admin = new HttpClient { BaseAddress = new Uri(fixture.KeycloakBaseUrl) };
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());
        var clientId = Guid.NewGuid().ToString();
        using var created = await admin.PostAsJsonAsync("/admin/realms/buddy-test/clients", new
        {
            clientId,
            publicClient = true,
            enabled = true,
            protocol = "openid-connect",
            defaultClientScopes = Array.Empty<string>(),
            optionalClientScopes = Array.Empty<string>()
        });
        created.EnsureSuccessStatusCode();

        var clock = NewClock();
        var cleaner = NewCleaner(clock);
        await cleaner.RunAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(30));
        await cleaner.RunAsync(CancellationToken.None);

        Assert.True(await fixture.KeycloakClientExistsAsync(clientId));
    }

    private async Task<string> AdminTokenAsync()
    {
        using var http = new HttpClient();
        using var response = await http.PostAsync($"{fixture.KeycloakBaseUrl}/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = "admin"
        }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
    }
}
