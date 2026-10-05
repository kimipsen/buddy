using Alba;

using buddy.Features.Babysitters;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline BabysitterListSnapshotProjection stays exactly consistent with a full replay
// after commands covering every event type -- see PickupScheduleSnapshotTests.
[Collection(BuddyApiCollection.Name)]
public sealed class BabysitterListSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna", "111");
        var bo = await BabysitterTestHelpers.AddAsync(fixture, token, "Bo");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Anne", ContactInfo = "222" }).ToUrl($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBeOk();
        });

        await BabysitterTestHelpers.ArchiveAsync(fixture, token, bo.Id);

        var store = fixture.Host.Services.GetRequiredService<IBabysitterListEventStore>();
        var id = BabysitterListId.ForGuardian(new UserId(guardianId));

        var replayed = BabysitterList.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(2, replayed.Babysitters.Count);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
