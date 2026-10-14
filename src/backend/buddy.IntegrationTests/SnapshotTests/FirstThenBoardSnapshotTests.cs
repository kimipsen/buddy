using buddy.Features.FirstThen;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FirstThen.FirstThenTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline snapshot (FirstThenBoardSnapshotProjection) must stay exactly equal to a full replay --
// see docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class FirstThenBoardSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_board_snapshot_matches_a_full_replay_after_every_kind_of_change()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await SetAsync(fixture, token, child.Id, Board(0));
        await ActAsync(fixture, token, child.Id, Complete, 1);
        await ActAsync(fixture, token, child.Id, Reopen, 1);
        await SetAsync(fixture, token, child.Id, Board(1, first: "Shoes on", then: "Play outside", thenMinutes: null));
        await ActAsync(fixture, token, child.Id, Complete, 2);
        await SetAsync(fixture, token, child.Id, Board(2, first: "HOMEWORK", then: "TV"));
        await ActAsync(fixture, token, child.Id, End, 3);

        var boards = fixture.Host.Services.GetRequiredService<IFirstThenBoardEventStore>();
        var id = FirstThenBoardId.ForChild(new UserId(child.Id));

        var replayed = FirstThenBoard.Rehydrate(await boards.ReadAsync(id, CancellationToken.None));
        var snapshot = await boards.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.Equal(FirstThenState.None, replayed.State);
        Assert.Equal(["HOMEWORK", "Shoes on"], replayed.RecentFirsts.Select(c => c.Title));
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
