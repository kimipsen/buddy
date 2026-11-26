using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.UpdateFlashCard;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateFlashCardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateFlashCard")]
    public async Task A_guardian_can_edit_a_card()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        var card = set.Cards.Single();

        var updated = (await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{card.Id}", new { Question = " New? ", Answer = " Yes " }))
            .ReadAsJson<FlashCardSetResponse>();

        var edited = Assert.Single(updated.Cards);
        Assert.Equal(card.Id, edited.Id);
        Assert.Equal("New?", edited.Question);
        Assert.Equal("Yes", edited.Answer);
    }

    [Fact]
    public async Task Repeating_the_same_edit_records_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        var card = set.Cards.Single();
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{card.Id}", new { card.Question, card.Answer });
        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{card.Id}", new { Question = "Changed?", Answer = "Yes" });
        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{card.Id}", new { Question = "Changed?", Answer = "Yes" });

        // Created, added, one update.
        Assert.Equal(3, (await store.ReadAsync(new FlashCardSetId(set.Id), CancellationToken.None)).Count);
    }

    [Theory]
    [InlineData("", "a")]
    [InlineData("q", "")]
    public async Task An_empty_question_or_answer_is_rejected(string question, string answer)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", new { Question = question, Answer = answer }, 400);
    }

    [Fact]
    public async Task An_unknown_card_or_set_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{Guid.CreateVersion7()}", new { Question = "q", Answer = "a" }, 404);
        await PutAsync(fixture, token, $"/flash-cards/sets/{Guid.CreateVersion7()}/cards/{set.Cards[0].Id}", new { Question = "q", Answer = "a" }, 404);
    }

    [Fact]
    public async Task A_card_of_a_deleted_set_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, token);
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", new { Question = "q", Answer = "a" }, 404);
    }
}
