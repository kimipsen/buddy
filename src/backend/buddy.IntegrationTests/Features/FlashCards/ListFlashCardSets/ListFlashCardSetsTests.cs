using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.ListFlashCardSets;

[Collection(BuddyApiCollection.Name)]
public sealed class ListFlashCardSetsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListFlashCardSets")]
    public async Task A_guardian_lists_their_own_sets_in_creation_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var first = await CreateTextSetAsync(fixture, token, "First");
        var second = await CreateMathSetAsync(fixture, token, "Second");

        var sets = (await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>();

        Assert.Equal([first.Id, second.Id], sets.Select(s => s.Id));
        Assert.Equal(FlashCardSetKind.Math, sets[1].Kind);
        Assert.True(sets[1].UniqueCardCount > 0);
    }

    [Fact]
    public async Task A_new_guardian_has_an_empty_library()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Empty((await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>());
    }

    [Fact]
    public async Task A_deleted_set_is_not_listed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var kept = await CreateTextSetAsync(fixture, token, "Kept");
        var gone = await CreateTextSetAsync(fixture, token, "Gone");

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{gone.Id}");

        var sets = (await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>();
        Assert.Equal([kept.Id], sets.Select(s => s.Id));
    }
}
