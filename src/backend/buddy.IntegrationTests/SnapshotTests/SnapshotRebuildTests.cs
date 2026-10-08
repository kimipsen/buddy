using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Backfill: a stream with no snapshot row (one that predates its projection, or a lost/corrupt
// row) gets one back from Marten's projection rebuild, which is what `task db:snapshots:rebuild`
// and `buddy projections rebuild --store <uri>` run. See "Backfilling existing streams" in
// docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class SnapshotRebuildTests(BuddyApiFixture fixture)
{
    private static readonly TimeSpan RebuildTimeout = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task A_missing_snapshot_is_restored_by_a_rebuild_and_matches_a_full_replay()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Rebuild Check");
        var id = new GroupId(groupId);

        var store = fixture.Host.Services.GetRequiredService<IGroupsStore>();
        await using (var session = store.LightweightSession())
        {
            session.Delete<GroupSnapshot>(groupId);
            await session.SaveChangesAsync();
        }

        var groups = fixture.Host.Services.GetRequiredService<IGroupEventStore>();
        Assert.Null(await groups.FindSnapshotAsync(id, CancellationToken.None));

        using var daemon = await store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<GroupSnapshotProjection>(RebuildTimeout, CancellationToken.None);

        var replayed = Group.Rehydrate(await groups.ReadAsync(id, CancellationToken.None));
        var snapshot = await groups.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    // Every registered projection, in every store, over whatever streams the test run has written
    // so far: catches a projection that inline appends accept but a rebuild can't replay.
    [Fact]
    public async Task Every_snapshot_projection_in_every_store_can_be_rebuilt()
    {
        var stores = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsInterface && typeof(IDocumentStore).IsAssignableFrom(t))
            .Select(t => (IDocumentStore)fixture.Host.Services.GetRequiredService(t))
            .ToList();

        var rebuilt = new List<string>();
        foreach (var store in stores)
        {
            var projections = store.Options.Events.Projections().Select(p => p.Name).ToList();
            if (projections.Count == 0)
            {
                continue;
            }

            using var daemon = await store.BuildProjectionDaemonAsync();
            foreach (var projection in projections)
            {
                await daemon.RebuildProjectionAsync(projection, RebuildTimeout, CancellationToken.None);
                rebuilt.Add(projection);
            }
        }

        Assert.Contains("GroupSnapshot", rebuilt);
        Assert.Contains("UserSnapshot", rebuilt);
    }
}
