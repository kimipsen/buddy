using buddy.Features.FirstThen;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FirstThen.FirstThenTestHelpers;

namespace buddy.IntegrationTests.Features.FirstThen.Lifecycle;

// Every row of the state diagram on the guardian's routes (first-then-board.md, Question 4),
// including the idempotent no-ops, which must append nothing.
[Collection(BuddyApiCollection.Name)]
public sealed class FirstThenLifecycleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CompleteFirstThenFirst")]
    [CoversEndpoint("ReopenFirstThenFirst")]
    [CoversEndpoint("EndFirstThenBoard")]
    public async Task A_guardian_walks_a_board_through_waiting_unlocked_waiting_and_none()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));

        var unlocked = await ActAsync(fixture, token, child.Id, Complete, 1);
        Assert.Equal("Unlocked", unlocked!.State);
        Assert.NotNull(unlocked.UnlockedAt);
        Assert.Equal(unlocked.UnlockedAt, unlocked.LastModifiedAt);

        var reopened = await ActAsync(fixture, token, child.Id, Reopen, 1);
        Assert.Equal("Waiting", reopened!.State);
        Assert.Null(reopened.UnlockedAt);

        var ended = await ActAsync(fixture, token, child.Id, End, 1);
        Assert.Equal("None", ended!.State);
        Assert.Null(ended.First);
        Assert.Null(ended.Then);
        Assert.Null(ended.ThenMinutes);
        // The revision and the recent cards survive the end.
        Assert.Equal(1, ended.Revision);
        Assert.Single((await GetAsync(fixture, token, child.Id))!.RecentFirsts);
    }

    [Fact]
    public async Task A_waiting_board_can_be_ended_without_being_completed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));

        Assert.Equal("None", (await ActAsync(fixture, token, child.Id, End, 1))!.State);
    }

    [Fact]
    public async Task Completing_an_unlocked_board_reopening_a_waiting_board_and_ending_no_board_append_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        // No stream at all yet.
        Assert.Equal("None", (await ActAsync(fixture, token, child.Id, End, 0))!.State);
        Assert.Empty(await ReadEventsAsync(child.Id));

        await SetAsync(fixture, token, child.Id, Board(0));
        await ActAsync(fixture, token, child.Id, Reopen, 1);
        var first = await ActAsync(fixture, token, child.Id, Complete, 1);
        var again = await ActAsync(fixture, token, child.Id, Complete, 1);
        await ActAsync(fixture, token, child.Id, End, 1);
        await ActAsync(fixture, token, child.Id, End, 1);

        Assert.Equal(first, again);
        Assert.Equal(
            ["FirstThenBoardStarted", "FirstThenBoardSet", "FirstThenFirstCompleted", "FirstThenBoardEnded"],
            (await ReadEventsAsync(child.Id)).Select(e => e.EventType));
    }

    [Fact]
    public async Task Completing_or_reopening_with_no_current_board_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await ActAsync(fixture, token, child.Id, Complete, 0, expectedStatus: 404);
        await ActAsync(fixture, token, child.Id, Reopen, 0, expectedStatus: 404);

        await SetAsync(fixture, token, child.Id, Board(0));
        await ActAsync(fixture, token, child.Id, End, 1);

        await ActAsync(fixture, token, child.Id, Complete, 1, expectedStatus: 404);
        await ActAsync(fixture, token, child.Id, Reopen, 1, expectedStatus: 404);
    }

    [Fact]
    public async Task A_state_change_naming_an_older_revision_is_a_conflict()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));
        await SetAsync(fixture, token, child.Id, Board(1, first: "Tidy your room"));

        await ActAsync(fixture, token, child.Id, Complete, 1, expectedStatus: 409);
        await ActAsync(fixture, token, child.Id, Reopen, 1, expectedStatus: 409);
        await ActAsync(fixture, token, child.Id, End, 1, expectedStatus: 409);
        await ActAsync(fixture, token, child.Id, Complete, 3, expectedStatus: 409);

        Assert.Equal("Waiting", (await GetAsync(fixture, token, child.Id))!.Board.State);
    }

    private async Task<IReadOnlyCollection<FirstThenEvent>> ReadEventsAsync(Guid childId) =>
        await fixture.Host.Services.GetRequiredService<IFirstThenBoardEventStore>()
            .ReadAsync(FirstThenBoardId.ForChild(new UserId(childId)), CancellationToken.None);
}
