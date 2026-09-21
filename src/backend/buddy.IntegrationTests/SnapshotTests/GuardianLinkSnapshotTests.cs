using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (GuardianLinkSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain the
// single source of truth and the snapshot is just a derived, quicker-to-read cache of the same
// state. GuardianLink's stream lives in the same Marten store/schema ("users") as User -- see
// MartenGuardianLinkEventStore -- which is why its snapshot projection is registered in
// UsersFeature rather than GuardiansFeature.
[Collection(BuddyApiCollection.Name)]
public sealed class GuardianLinkSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        var guardianLinks = fixture.Host.Services.GetRequiredService<IGuardianLinkEventStore>();
        var id = new GuardianLinkId(child.GuardianLinkId);

        var events = await guardianLinks.ReadAsync(id, CancellationToken.None);
        var replayed = GuardianLink.Rehydrate(events);
        var snapshot = await guardianLinks.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
