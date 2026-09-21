using Alba;

using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (PickupScheduleSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain the
// single source of truth and the snapshot is just a derived, quicker-to-read cache of the same
// state.
[Collection(BuddyApiCollection.Name)]
public sealed class PickupScheduleSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.SelfEscort })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Playdate, PlaydateHostName = "Mia's mom" })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBe(204);
        });

        var pickups = fixture.Host.Services.GetRequiredService<IPickupScheduleEventStore>();
        var id = await pickups.FindIdForChildAsync(new UserId(child.Id), CancellationToken.None);

        Assert.NotNull(id);

        var events = await pickups.ReadAsync(id!, CancellationToken.None);
        var replayed = PickupSchedule.Rehydrate(events);
        var snapshot = await pickups.FindSnapshotAsync(id!, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
