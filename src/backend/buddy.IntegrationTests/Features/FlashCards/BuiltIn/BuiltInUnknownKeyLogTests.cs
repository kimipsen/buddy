using buddy.Features.FlashCards;
using buddy.IntegrationTests.Common;
using buddy.IntegrationTests.Fixtures;

using Alba;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The warning for a rejected built-in card key. A second host with a capturing logger: the shared one
// does not capture logs. The state is in Postgres, so setup runs on the shared host and only the
// finish goes through this one.
[Collection(BuddyApiCollection.Name)]
public sealed class BuiltInUnknownKeyLogTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const string ForgedKey = "forged-secret-card-key";

    private static readonly CapturingLoggerProvider Logs = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        Logs.Clear();
        _host = await fixture.SharedHostAsync(
            nameof(BuiltInUnknownKeyLogTests),
            new Dictionary<string, string?>(),
            services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IBuiltInCatalogue>(FixtureBuiltInCatalogue.Instance));
                services.AddSingleton<ILoggerProvider>(Logs);
            });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_rejected_key_logs_a_warning_with_the_set_key_and_slot_but_not_the_client_key()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, FixtureBuiltInCatalogue.ClockId.Value);
        var deck = await GetDeckAsync(fixture, childToken, FixtureBuiltInCatalogue.ClockId.Value, enabled.Tiers[0].Id);
        object[] results = [.. deck.Cards.Select((card, slot) => new { Slot = slot, CardKey = slot == 2 ? ForgedKey : card.Key, KnewFirstTime = true })];
        Logs.Clear();

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { GameId = Guid.NewGuid(), SetId = FixtureBuiltInCatalogue.ClockId.Value, TierId = enabled.Tiers[0].Id, Results = results }).ToUrl("/flash-cards/me/games");
            _.StatusCodeShouldBe(400);
        });

        var entry = Assert.Single(Logs.Entries, e => e.EventId == 16003);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("fixture:clock", entry.Property("BuiltInKey"));
        Assert.Equal(2, entry.Property("Slot"));
        Assert.Contains("fixture:clock", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ForgedKey, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Logs.Entries, e => e.Message.Contains(ForgedKey, StringComparison.Ordinal));
    }
}
