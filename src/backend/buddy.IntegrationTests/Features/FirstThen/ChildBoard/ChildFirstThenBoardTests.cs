using buddy.Features.FirstThen;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.FirstThen.FirstThenTestHelpers;

namespace buddy.IntegrationTests.Features.FirstThen.ChildBoard;

// The child's own /me routes (first-then-board.md, Question 5): read the board, tick the First,
// undo it and end the board, each naming the revision they were shown.
[Collection(BuddyApiCollection.Name)]
public sealed class ChildFirstThenBoardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetMyFirstThenBoard")]
    [CoversEndpoint("CompleteMyFirstThenFirst")]
    [CoversEndpoint("ReopenMyFirstThenFirst")]
    [CoversEndpoint("EndMyFirstThenBoard")]
    public async Task A_child_sees_their_board_ticks_the_first_undoes_it_and_ends_it()
    {
        var guardian = await fixture.CreateUserAsync(givenName: "Sara");
        var guardianToken = await fixture.GetAccessTokenAsync(guardian);
        await fixture.GetUserIdAsync(guardianToken);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await SetAsync(fixture, guardianToken, child.Id, Board(0));

        var mine = await GetMineAsync(fixture, childToken);
        Assert.Equal("Waiting", mine!.State);
        Assert.Equal(new FirstThenCardDto("Homework", "📚"), mine.First);
        // The guardian's name is for the guardian page only.
        Assert.Null(mine.LastModifiedByName);

        var unlocked = await ActAsync(fixture, childToken, null, Complete, 1);
        Assert.Equal("Unlocked", unlocked!.State);
        Assert.NotNull(unlocked.UnlockedAt);
        Assert.Null(unlocked.LastModifiedByName);

        // The guardian sees who ticked it.
        var seen = await GetAsync(fixture, guardianToken, child.Id);
        Assert.Equal("Unlocked", seen!.Board.State);
        Assert.Equal("Emil", seen.Board.LastModifiedByName);

        Assert.Equal("Waiting", (await ActAsync(fixture, childToken, null, Reopen, 1))!.State);
        Assert.Equal("None", (await ActAsync(fixture, childToken, null, End, 1))!.State);
        Assert.Equal("None", (await GetMineAsync(fixture, childToken))!.State);
    }

    [Fact]
    public async Task A_negative_revision_is_rejected_on_the_me_routes()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await ActAsync(fixture, childToken, null, Complete, -1, expectedStatus: 400);
        await ActAsync(fixture, childToken, null, Reopen, -1, expectedStatus: 400);
        await ActAsync(fixture, childToken, null, End, -1, expectedStatus: 400);
    }

    [Fact]
    public async Task A_child_with_no_board_reads_as_none()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        Assert.Equal(new FirstThenBoardDto(0, "None", null, null, null, null, null, null), await GetMineAsync(fixture, childToken));
        await ActAsync(fixture, childToken, null, Complete, 0, expectedStatus: 404);
    }

    [Fact]
    public async Task A_child_ticking_a_board_a_guardian_has_just_replaced_gets_a_conflict_and_the_new_board_is_untouched()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await SetAsync(fixture, guardianToken, child.Id, Board(0));
        await SetAsync(fixture, guardianToken, child.Id, Board(1, first: "Tidy your room", firstIcon: "🧹"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { Revision = 1 }).ToUrl("/first-then/me/first/complete");
            _.StatusCodeShouldBe(409);
        });

        Assert.Contains(FirstThenWriteOutcome.BoardChangedCode, await response.ReadAsTextAsync(), StringComparison.Ordinal);

        var mine = await GetMineAsync(fixture, childToken);
        Assert.Equal(2, mine!.Revision);
        Assert.Equal("Waiting", mine.State);
        Assert.Equal("Tidy your room", mine.First!.Title);
    }

    [Fact]
    public async Task An_adult_with_no_guardian_links_gets_not_found_on_the_me_routes()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await GetMineAsync(fixture, token, expectedStatus: 404);
        await ActAsync(fixture, token, null, Complete, 0, expectedStatus: 404);
        await ActAsync(fixture, token, null, Reopen, 0, expectedStatus: 404);
        await ActAsync(fixture, token, null, End, 0, expectedStatus: 404);
    }

    [Fact]
    public async Task A_guardian_on_the_me_routes_does_not_reach_their_childs_board()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        await SetAsync(fixture, guardianToken, child.Id, Board(0));

        // A guardian has no guardian of their own, so /me is not theirs to use.
        await GetMineAsync(fixture, guardianToken, expectedStatus: 404);
    }
}
