using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.ListMyFlashCardSets;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMyFlashCardSetsTests(BuddyApiFixture fixture)
{
    private static async Task<IReadOnlyList<MyFlashCardSetResponse>> ListAsync(BuddyApiFixture fixture, string token) =>
        (await GetAsync(fixture, token, "/flash-cards/me/sets")).ReadAsJson<List<MyFlashCardSetResponse>>() ?? [];

    [Fact]
    [CoversEndpoint("ListMyFlashCardSets")]
    public async Task A_child_sees_their_enabled_sets_with_tiers_and_card_count()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken, "Capitals");
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var mine = Assert.Single(await ListAsync(fixture, childToken));

        Assert.Equal(set.Id, mine.SetId);
        Assert.Equal("Capitals", mine.SetName);
        Assert.Equal(FlashCardSetKind.Text, mine.Kind);
        Assert.Equal(1, mine.UniqueCardCount);
        Assert.Equal([(5, 1), (10, 3)], mine.Tiers.Select(t => (t.CardCount, t.Stars)));
    }

    [Fact]
    public async Task A_child_with_nothing_enabled_gets_an_empty_list()
    {
        var (_, _, _, childToken) = await CreateFamilyAsync(fixture);

        Assert.Empty(await ListAsync(fixture, childToken));
    }

    [Fact]
    public async Task A_deleted_set_drops_out_of_the_list()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var kept = await CreatePlayableSetAsync(fixture, guardianToken, "Kept");
        var deleted = await CreatePlayableSetAsync(fixture, guardianToken, "Deleted");
        await EnableAsync(fixture, guardianToken, child.Id, kept.Id);
        await EnableAsync(fixture, guardianToken, child.Id, deleted.Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{deleted.Id}");

        var mine = Assert.Single(await ListAsync(fixture, childToken));

        Assert.Equal(kept.Id, mine.SetId);
    }

    [Fact]
    public async Task A_child_only_sees_their_own_sets()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var (_, _, _, otherChildToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        Assert.Empty(await ListAsync(fixture, otherChildToken));
    }

    [Fact]
    public async Task A_guardian_gets_not_found_on_the_play_route()
    {
        var (guardianToken, _, _, _) = await CreateFamilyAsync(fixture);

        await GetAsync(fixture, guardianToken, "/flash-cards/me/sets", 404);
    }
}
