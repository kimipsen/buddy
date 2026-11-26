using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.RemoveFlashCard;

[Collection(BuddyApiCollection.Name)]
public sealed class RemoveFlashCardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RemoveFlashCard")]
    public async Task A_guardian_can_remove_a_card()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        var second = await AddCardAsync(fixture, token, set.Id, "Two?", "2");

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}");

        var remaining = (await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>().Single(s => s.Id == set.Id);
        Assert.Equal([second.Cards[1].Id], remaining.Cards.Select(c => c.Id));
    }

    [Fact]
    public async Task Removing_an_unknown_or_already_removed_card_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        var cardId = set.Cards[0].Id;

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{cardId}");
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{cardId}", 404);
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{Guid.CreateVersion7()}/cards/{cardId}", 404);
    }

    [Fact]
    public async Task A_card_of_a_deleted_set_cannot_be_removed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", 404);
    }
}
