using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (GroupSnapshotProjection, schema "snapshots") stays exactly
// consistent with a full replay-from-events rehydration after a sequence of commands -- the
// invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain the
// single source of truth and the snapshot is just a derived, quicker-to-read cache of the same
// state.
[Collection(BuddyApiCollection.Name)]
public sealed class GroupSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Snapshot Check");

        var (_, _, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var groups = fixture.Host.Services.GetRequiredService<IGroupEventStore>();
        var id = new GroupId(groupId);

        var events = await groups.ReadAsync(id, CancellationToken.None);
        var replayed = Group.Rehydrate(events);
        var snapshot = await groups.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
