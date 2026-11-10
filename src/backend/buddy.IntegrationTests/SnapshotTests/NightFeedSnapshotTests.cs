using buddy.Features.NightDuty;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.NightDuty;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline NightFeedSnapshotProjection stays equal to a full replay after issuing and
// revoking tokens (see docs/backend/analysis/event-stream-snapshots.md).
[Collection(BuddyApiCollection.Name)]
public sealed class NightFeedSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var bea = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bea");

        var first = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, alex.Id);
        await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, alex.Id);
        await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, bea.Id);
        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, alex.Id, first.TokenId);

        var feeds = fixture.Host.Services.GetRequiredService<INightFeedEventStore>();
        var id = NightFeedId.ForGuardian(new UserId(guardianId));

        var replayed = NightFeed.Rehydrate(await feeds.ReadAsync(id, CancellationToken.None));
        var snapshot = await feeds.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Tokens.Count);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
