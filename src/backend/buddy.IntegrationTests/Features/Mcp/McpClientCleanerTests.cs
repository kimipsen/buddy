using System.Globalization;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// The cleanup's rules and failsafes against an in-memory registry (no containers): what is deleted when, and
// what is kept whenever the pass can't prove a client abandoned. McpClientCleanupTests and
// McpKeycloakClientRegistryTests cover the same job and the registry against the real Keycloak.
public sealed class McpClientCleanerTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeRegistry _registry = new();
    private readonly FakeTimeProvider _clock = new(Start);
    private readonly CapturingLoggerProvider _logs = new();

    private McpClientCleaner Cleaner(bool dryRun = false, int max = 100) =>
        new(_registry, _clock, Options.Create(new McpClientCleanupOptions { DryRun = dryRun, MaxDeletesPerPass = max }),
            new LoggerFactory([_logs]).CreateLogger<McpClientCleaner>());

    private KeycloakClientSummary Registered(string? firstSeen = null, string scope = "buddy-mcp:read")
    {
        var client = new KeycloakClientSummary($"id-{_registry.Clients.Count}", Guid.NewGuid().ToString(), true, ["basic", scope], firstSeen);
        _registry.Clients.Add(client);
        return client;
    }

    private bool Exists(KeycloakClientSummary client) => _registry.Clients.Any(c => c.Id == client.Id);

    private static string Ago(TimeSpan age) => (Start - age).ToString("O", CultureInfo.InvariantCulture);

    [Fact]
    public async Task The_age_sequence_stamps_on_the_first_pass_keeps_at_6_days_and_deletes_at_7()
    {
        var client = Registered();
        var cleaner = Cleaner();

        var first = await cleaner.RunAsync(CancellationToken.None);
        Assert.Equal(1, first.Stamped);
        Assert.Equal(0, first.Deleted);
        Assert.True(Exists(client));
        Assert.Equal(Start.ToString("O", CultureInfo.InvariantCulture), _registry.Attribute(client));

        _clock.Advance(TimeSpan.FromDays(6));
        var second = await cleaner.RunAsync(CancellationToken.None);
        Assert.Equal(0, second.Deleted);
        Assert.Equal(1, second.Kept);
        Assert.True(Exists(client));

        _clock.Advance(TimeSpan.FromDays(1));
        var third = await cleaner.RunAsync(CancellationToken.None);
        Assert.Equal(1, third.Deleted);
        Assert.False(Exists(client));
    }

    [Fact]
    public async Task Only_the_consenting_users_clients_are_kept()
    {
        var used = Registered(Ago(TimeSpan.FromDays(30)));
        var usedByAnother = Registered(Ago(TimeSpan.FromDays(30)));
        var abandoned = Registered(Ago(TimeSpan.FromDays(30)));
        _registry.Users["u1"] = [used.ClientId];
        _registry.Users["u2"] = [usedByAnother.ClientId, "buddy-frontend"];

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(1, result.Deleted);
        Assert.True(Exists(used));
        Assert.True(Exists(usedByAnother));
        Assert.False(Exists(abandoned));
        Assert.Equal(0, result.Stamped);
    }

    [Fact]
    public async Task A_client_whose_only_consenting_user_was_deleted_is_deleted_once_it_is_past_the_grace_period()
    {
        var client = Registered(Ago(TimeSpan.FromDays(30)));
        _registry.Users["guardian"] = [client.ClientId];
        var cleaner = Cleaner();

        await cleaner.RunAsync(CancellationToken.None);
        Assert.True(Exists(client));

        _registry.Users.Remove("guardian");
        await cleaner.RunAsync(CancellationToken.None);
        Assert.False(Exists(client));
    }

    [Fact]
    public async Task A_delete_that_fails_for_one_client_does_not_end_the_pass()
    {
        var broken = Registered(Ago(TimeSpan.FromDays(30)));
        var fine = Registered(Ago(TimeSpan.FromDays(30)));
        _registry.FailDeleteFor = broken.Id;

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(1, result.Deleted);
        Assert.Equal(1, result.Failed);
        Assert.True(Exists(broken));
        Assert.False(Exists(fine));
        Assert.Contains(_logs.Entries, entry => entry.EventId == 15008 && entry.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("buddy-mcp", true, "buddy-mcp:read", false)]
    [InlineData("buddy-frontend", true, "buddy-mcp:read", false)]
    [InlineData("buddy-admin-cli", false, "buddy-mcp:read", false)]
    [InlineData("account", true, "buddy-mcp:read", false)]
    [InlineData("security-admin-console", true, "buddy-mcp:read", false)]
    [InlineData("my-own-client", true, "buddy-mcp:read", false)]
    [InlineData("buddy-0b1c6e0c-6f0e-4f6b-9c06-1f3a6c1e7b11", true, "buddy-mcp:read", false)]
    [InlineData("0b1c6e0c-6f0e-4f6b-9c06-1f3a6c1e7b11", false, "buddy-mcp:read", false)]
    [InlineData("0b1c6e0c-6f0e-4f6b-9c06-1f3a6c1e7b11", true, "profile", false)]
    [InlineData("0b1c6e0c-6f0e-4f6b-9c06-1f3a6c1e7b11", true, "buddy-mcp:read", true)]
    public void Only_a_public_UUID_client_with_a_buddy_mcp_scope_is_a_registered_one(string clientId, bool isPublic, string scope, bool expected) =>
        Assert.Equal(expected, McpClientCleaner.IsRegisteredClient(new KeycloakClientSummary("x", clientId, isPublic, [scope], null)));

    [Fact]
    public async Task Built_in_and_pre_registered_clients_are_never_touched()
    {
        foreach (var clientId in new[] { "buddy-mcp", "buddy-frontend", "buddy-admin-cli", "account", "broker", "realm-management" })
        {
            _registry.Clients.Add(new KeycloakClientSummary($"id-{clientId}", clientId, true, ["buddy-mcp:read"], Ago(TimeSpan.FromDays(400))));
        }

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(0, result.Candidates);
        Assert.Empty(_registry.Deleted);
        Assert.Equal(6, _registry.Clients.Count);
    }

    [Fact]
    public async Task A_summary_line_reports_the_pass()
    {
        Registered(Ago(TimeSpan.FromDays(30)));
        Registered();

        await Cleaner().RunAsync(CancellationToken.None);

        var summary = Assert.Single(_logs.Entries, entry => entry.EventId == 15003);
        Assert.Equal(LogLevel.Information, summary.Level);
        Assert.Equal(false, summary.Property("DryRun"));
        Assert.Equal(2, summary.Property("Candidates"));
        Assert.Equal(1, summary.Property("Deleted"));
        Assert.Equal(1, summary.Property("Stamped"));
    }

    [Fact]
    public async Task Without_a_registered_client_the_users_and_consents_are_not_read()
    {
        _registry.FailCount = true;
        _registry.Clients.Add(new KeycloakClientSummary("a", "buddy-mcp", true, [], null));

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, _registry.CountCalls);
    }

    // Fail closed: one case per signal. Nothing is deleted and every client is kept, counted as unknown.
    [Theory]
    [InlineData("consents-throw")]
    [InlineData("count-throws")]
    [InlineData("users-fewer-than-count")]
    [InlineData("users-more-than-count")]
    public async Task A_consent_lookup_that_cannot_be_trusted_keeps_every_client(string failure)
    {
        var old = Registered(Ago(TimeSpan.FromDays(30)));
        var older = Registered(Ago(TimeSpan.FromDays(300)));
        _registry.Users["u1"] = ["unrelated"];
        _registry.Users["u2"] = [];
        switch (failure)
        {
            case "consents-throw":
                _registry.FailConsentsFor = "u2";
                break;
            case "count-throws":
                _registry.FailCount = true;
                break;
            case "users-fewer-than-count":
                _registry.CountOffset = 1;
                break;
            default:
                _registry.CountOffset = -1;
                break;
        }

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(2, result.KeptUnknown);
        Assert.Equal(0, result.Deleted);
        Assert.Empty(_registry.Deleted);
        Assert.True(Exists(old) && Exists(older));
        Assert.Contains(_logs.Entries, entry => entry.EventId == 15006 && entry.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("2099-01-01T00:00:00.0000000+00:00")]
    public async Task A_stamp_that_does_not_parse_or_lies_in_the_future_keeps_the_client_as_unknown(string stamp)
    {
        var client = Registered(stamp);

        var result = await Cleaner().RunAsync(CancellationToken.None);

        Assert.Equal(1, result.KeptUnknown);
        Assert.Equal(0, result.Deleted);
        Assert.True(Exists(client));
    }

    [Fact]
    public async Task Exactly_the_cap_deletes_all_without_an_error()
    {
        for (var n = 0; n < 3; n++)
        {
            Registered(Ago(TimeSpan.FromDays(30)));
        }

        var result = await Cleaner(max: 3).RunAsync(CancellationToken.None);

        Assert.Equal(3, result.Deleted);
        Assert.Empty(_registry.Clients);
        Assert.DoesNotContain(_logs.Entries, entry => entry.EventId == 15005);
    }

    [Fact]
    public async Task One_more_than_the_cap_deletes_the_cap_and_logs_an_error()
    {
        for (var n = 0; n < 4; n++)
        {
            Registered(Ago(TimeSpan.FromDays(30)));
        }

        var result = await Cleaner(max: 3).RunAsync(CancellationToken.None);

        Assert.Equal(3, result.Deleted);
        Assert.Equal(1, result.SkippedByCap);
        Assert.Single(_registry.Clients);
        var error = Assert.Single(_logs.Entries, entry => entry.EventId == 15005);
        Assert.Equal(LogLevel.Error, error.Level);

        // The next pass takes the rest.
        Assert.Equal(1, (await Cleaner(max: 3).RunAsync(CancellationToken.None)).Deleted);
    }

    [Fact]
    public async Task A_dry_run_stamps_on_the_first_pass_lists_what_it_would_delete_and_never_deletes()
    {
        var client = Registered();
        var dry = Cleaner(dryRun: true);

        var first = await dry.RunAsync(CancellationToken.None);
        Assert.Equal(1, first.Stamped);
        Assert.NotNull(_registry.Attribute(client));

        _clock.Advance(TimeSpan.FromDays(7));
        var second = await dry.RunAsync(CancellationToken.None);

        Assert.Equal(0, second.Deleted);
        Assert.Equal([client.ClientId], second.WouldDelete);
        Assert.Empty(_registry.Deleted);
        var summary = _logs.Entries.Last(entry => entry.EventId == 15003);
        Assert.Equal(true, summary.Property("DryRun"));
        Assert.Contains(client.ClientId, (string)summary.Property("WouldDeleteClientIds")!);

        // The same state with DryRun off deletes.
        var live = await Cleaner().RunAsync(CancellationToken.None);
        Assert.Equal(1, live.Deleted);
        Assert.False(Exists(client));
    }

    [Fact]
    public async Task Would_be_deletions_count_against_the_cap_so_a_dry_run_shows_it_being_hit()
    {
        for (var n = 0; n < 4; n++)
        {
            Registered(Ago(TimeSpan.FromDays(30)));
        }

        var result = await Cleaner(dryRun: true, max: 3).RunAsync(CancellationToken.None);

        Assert.Equal(3, result.WouldDelete.Count);
        Assert.Equal(1, result.SkippedByCap);
        Assert.Contains(_logs.Entries, entry => entry.EventId == 15005);
        Assert.Equal(4, _registry.Clients.Count);
    }

    private sealed class FakeRegistry : IKeycloakClientRegistry
    {
        public List<KeycloakClientSummary> Clients { get; } = [];
        public Dictionary<string, string[]> Users { get; } = [];
        public List<string> Deleted { get; } = [];
        public string? FailDeleteFor { get; set; }
        public string? FailConsentsFor { get; set; }
        public bool FailCount { get; set; }
        public int CountOffset { get; set; }
        public int CountCalls { get; private set; }

        public string? Attribute(KeycloakClientSummary client) => Clients.Single(c => c.Id == client.Id).FirstSeen;

        public Task<IReadOnlyList<KeycloakClientSummary>> ListClientsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KeycloakClientSummary>>([.. Clients]);

        public Task<int> CountUsersAsync(CancellationToken cancellationToken)
        {
            CountCalls++;
            return FailCount ? throw new HttpRequestException("503") : Task.FromResult(Users.Count + CountOffset);
        }

        public Task<IReadOnlyList<string>> ListUserIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([.. Users.Keys]);

        public Task<IReadOnlyList<string>> ListConsentedClientIdsAsync(string userId, CancellationToken cancellationToken) =>
            userId == FailConsentsFor
                ? throw new InvalidOperationException("unparsable consents")
                : Task.FromResult<IReadOnlyList<string>>(Users[userId]);

        public Task SetClientAttributeAsync(string id, string name, string value, CancellationToken cancellationToken)
        {
            Assert.Equal(KeycloakClientRegistry.FirstSeenAttribute, name);
            var index = Clients.FindIndex(client => client.Id == id);
            Clients[index] = Clients[index] with { FirstSeen = value };
            return Task.CompletedTask;
        }

        public Task DeleteClientAsync(string id, CancellationToken cancellationToken)
        {
            if (id == FailDeleteFor)
            {
                throw new HttpRequestException("500");
            }

            Clients.RemoveAll(client => client.Id == id);
            Deleted.Add(id);
            return Task.CompletedTask;
        }
    }
}
