using buddy.Features.Medicines;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (MedicineSharingSnapshotProjection, schema "snapshots")
// stays exactly consistent with a full replay-from-events rehydration -- including through a
// re-share, which exercises MedicineSharedWithGroup's dual role as both the stream's creation
// event (MedicineSharingSnapshotProjection.Create, dispatched by Marten's generated Evolver for
// the first occurrence on a stream) and a later update (MedicineSharingSnapshotProjection.Apply,
// once a snapshot row already exists), mirroring MedicineSharing.Start vs
// MedicineSharing.Advance. See
// docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class MedicineSharingSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_through_unshare_and_reshare()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        // First MedicineSharedWithGroup on a fresh stream -- exercises Create.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        // Re-share: a second MedicineSharedWithGroup on the same stream -- exercises Apply, not
        // Create, since a snapshot row already exists.
        var otherGroupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Grandparents");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{child.Id}/group-share/{otherGroupId}");
            _.StatusCodeShouldBe(204);
        });

        var sharing = fixture.Host.Services.GetRequiredService<IMedicineSharingEventStore>();
        var sharingId = await sharing.FindIdForChildAsync(new UserId(child.Id), CancellationToken.None);
        Assert.NotNull(sharingId);

        var events = await sharing.ReadAsync(sharingId, CancellationToken.None);
        var replayed = MedicineSharing.Rehydrate(events);
        var snapshot = await sharing.FindSnapshotAsync(sharingId, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
