using buddy.Features.FirstThen;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FirstThen.FirstThenTestHelpers;

namespace buddy.IntegrationTests.Features.FirstThen.GuardianBoard;

// The guardian's /children/{childId} routes: GetFirstThenBoard and SetFirstThenBoard, plus the
// authorization matrix (docs/backend/analysis/first-then-board.md, Question 5).
[Collection(BuddyApiCollection.Name)]
public sealed class GuardianFirstThenBoardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetFirstThenBoard")]
    [CoversEndpoint("GetFirstThenBoard")]
    public async Task A_guardian_sets_a_board_and_reads_it_back_with_their_name()
    {
        var guardian = await fixture.CreateUserAsync(givenName: "Sara");
        var token = await fixture.GetAccessTokenAsync(guardian);
        await fixture.GetUserIdAsync(token);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");

        var set = await SetAsync(fixture, token, child.Id, Board(0, first: "  Maths p. 12  "));

        Assert.NotNull(set);
        Assert.Equal(1, set.Revision);
        Assert.Equal("Waiting", set.State);
        Assert.Equal(new FirstThenCardDto("Maths p. 12", "📚"), set.First);
        Assert.Equal(new FirstThenCardDto("iPad", "📱"), set.Then);
        Assert.Equal(20, set.ThenMinutes);
        Assert.Null(set.UnlockedAt);
        Assert.Equal("Sara", set.LastModifiedByName);

        var read = await GetAsync(fixture, token, child.Id);

        Assert.Equal(set, read!.Board);
        Assert.Equal([new FirstThenCardDto("Maths p. 12", "📚")], read.RecentFirsts);
        Assert.Equal([new FirstThenCardDto("iPad", "📱")], read.RecentThens);
    }

    [Fact]
    public async Task A_child_with_no_board_reads_as_none_not_404()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var read = await GetAsync(fixture, token, child.Id);

        Assert.Equal(new FirstThenBoardDto(0, "None", null, null, null, null, null, null), read!.Board);
        Assert.Empty(read.RecentFirsts);
        Assert.Empty(read.RecentThens);
    }

    [Fact]
    public async Task A_new_board_replaces_the_current_one_and_starts_waiting_again()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));
        await ActAsync(fixture, token, child.Id, Complete, 1);

        var replaced = await SetAsync(fixture, token, child.Id, Board(1, then: "TV", thenIcon: "📺", thenMinutes: null));

        Assert.Equal(2, replaced!.Revision);
        Assert.Equal("Waiting", replaced.State);
        Assert.Null(replaced.UnlockedAt);
        Assert.Null(replaced.ThenMinutes);
        Assert.Equal(new FirstThenCardDto("TV", "📺"), replaced.Then);
    }

    [Fact]
    public async Task Recent_cards_are_newest_first_distinct_by_title_and_capped_at_eight()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        for (var i = 0; i < 9; i++)
        {
            await SetAsync(fixture, token, child.Id, Board(i, first: $"Task {i}", then: "iPad"));
        }

        // The same title in another case moves the card to the front instead of adding it twice.
        await SetAsync(fixture, token, child.Id, Board(9, first: "task 3", then: "IPAD", thenIcon: "🎮"));

        var read = await GetAsync(fixture, token, child.Id);

        Assert.Equal(["task 3", "Task 8", "Task 7", "Task 6", "Task 5", "Task 4", "Task 2", "Task 1"], read!.RecentFirsts.Select(c => c.Title));
        Assert.Equal([new FirstThenCardDto("IPAD", "🎮")], read.RecentThens);
    }

    [Fact]
    public async Task Two_guardians_setting_from_the_same_revision_the_second_gets_a_conflict()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await SetAsync(fixture, family.FirstToken, family.Child.Id, Board(0, then: "TV"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Put.Json(Board(0, then: "Lego")).ToUrl($"/first-then/children/{family.Child.Id}");
            _.StatusCodeShouldBe(409);
        });

        Assert.Contains(FirstThenWriteOutcome.BoardChangedCode, await response.ReadAsTextAsync(), StringComparison.Ordinal);
        Assert.Equal("TV", (await GetAsync(fixture, family.SecondToken, family.Child.Id))!.Board.Then!.Title);
    }

    [Theory]
    [InlineData("", "📚", 20)]
    [InlineData("   ", "📚", 20)]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901", "📚", 20)]
    [InlineData("Homework", "", 20)]
    [InlineData("Homework", "📚📚📚📚📚📚📚📚📚", 20)]
    [InlineData("Homework", "📚", 4)]
    [InlineData("Homework", "📚", 121)]
    public async Task An_invalid_board_is_rejected(string firstTitle, string firstIcon, int thenMinutes)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await SetAsync(fixture, token, child.Id, Board(0, first: firstTitle, firstIcon: firstIcon, thenMinutes: thenMinutes), expectedStatus: 400);
    }

    [Fact]
    public async Task A_sixty_character_title_and_the_length_bounds_are_accepted_and_first_and_then_may_share_a_title()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await SetAsync(fixture, token, child.Id, Board(0, first: new string('x', 60), thenMinutes: 5));
        await SetAsync(fixture, token, child.Id, Board(1, first: "Lego", then: "Lego", thenMinutes: 120));
    }

    [Fact]
    public async Task A_negative_revision_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await SetAsync(fixture, token, child.Id, Board(-1), expectedStatus: 400);
        await ActAsync(fixture, token, child.Id, Complete, -1, expectedStatus: 400);
        await ActAsync(fixture, token, child.Id, Reopen, -1, expectedStatus: 400);
        await ActAsync(fixture, token, child.Id, End, -1, expectedStatus: 400);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found_on_every_guardian_route()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GetAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
        await SetAsync(fixture, strangerToken, child.Id, Board(1), expectedStatus: 404);
        await ActAsync(fixture, strangerToken, child.Id, Complete, 1, expectedStatus: 404);
        await ActAsync(fixture, strangerToken, child.Id, Reopen, 1, expectedStatus: 404);
        await ActAsync(fixture, strangerToken, child.Id, End, 1, expectedStatus: 404);
    }

    [Fact]
    public async Task The_child_is_forbidden_on_the_guardian_routes()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        await SetAsync(fixture, token, child.Id, Board(0));
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await GetAsync(fixture, childToken, child.Id, expectedStatus: 403);
        await SetAsync(fixture, childToken, child.Id, Board(1), expectedStatus: 403);
        await ActAsync(fixture, childToken, child.Id, Complete, 1, expectedStatus: 403);
    }

    [Fact]
    public async Task A_revoked_guardian_drops_to_not_found_and_the_board_stays_for_the_other_guardian()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await SetAsync(fixture, family.FirstToken, family.Child.Id, Board(0));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.FirstToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await GetAsync(fixture, family.FirstToken, family.Child.Id, expectedStatus: 404);
        await ActAsync(fixture, family.FirstToken, family.Child.Id, End, 1, expectedStatus: 404);
        Assert.Equal("Waiting", (await GetAsync(fixture, family.SecondToken, family.Child.Id))!.Board.State);
    }

    [Fact]
    public async Task The_first_set_starts_the_stream_with_its_started_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await SetAsync(fixture, token, child.Id, Board(0));

        var boards = fixture.Host.Services.GetRequiredService<IFirstThenBoardEventStore>();
        var events = await boards.ReadAsync(FirstThenBoardId.ForChild(new UserId(child.Id)), CancellationToken.None);

        Assert.Equal(["FirstThenBoardStarted", "FirstThenBoardSet"], events.Select(e => e.EventType));
    }
}
