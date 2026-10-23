using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Pickups;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline PickupFeedSnapshotProjection stays equal to a full replay after issuing and
// revoking tokens (see docs/backend/analysis/event-stream-snapshots.md).
[Collection(BuddyApiCollection.Name)]
public sealed class PickupFeedSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var first = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/pickups/me/ical-tokens/{first.TokenId}");
            _.StatusCodeShouldBe(204);
        });

        var feeds = fixture.Host.Services.GetRequiredService<IPickupFeedEventStore>();
        var id = PickupFeedId.ForGuardian(new UserId(guardianId));

        var replayed = PickupFeed.Rehydrate(await feeds.ReadAsync(id, CancellationToken.None));
        var snapshot = await feeds.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Tokens);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
