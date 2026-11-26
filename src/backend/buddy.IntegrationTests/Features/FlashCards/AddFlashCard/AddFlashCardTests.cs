using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.AddFlashCard;

[Collection(BuddyApiCollection.Name)]
public sealed class AddFlashCardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AddFlashCard")]
    public async Task A_guardian_can_add_a_card_to_a_text_set()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);

        var updated = (await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = " Capital of Denmark? ", Answer = " Copenhagen " }))
            .ReadAsJson<FlashCardSetResponse>();

        var card = Assert.Single(updated.Cards);
        Assert.Equal("Capital of Denmark?", card.Question);
        Assert.Equal("Copenhagen", card.Answer);
        Assert.Equal(1, updated.UniqueCardCount);
    }

    [Fact]
    public async Task Adding_cards_keeps_their_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);

        await AddCardAsync(fixture, token, set.Id, "One?", "1");
        var updated = await AddCardAsync(fixture, token, set.Id, "Two?", "2");

        Assert.Equal(["One?", "Two?"], updated.Cards.Select(c => c.Question));
    }

    [Fact]
    public async Task A_card_cannot_be_added_to_a_math_set()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateMathSetAsync(fixture, token);

        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = "q", Answer = "a" }, 400);
    }

    [Theory]
    [InlineData("", "a")]
    [InlineData("q", "")]
    public async Task An_empty_question_or_answer_is_rejected(string question, string answer)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);

        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = question, Answer = answer }, 400);
    }

    [Fact]
    public async Task Text_over_the_limit_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        var tooLong = new string('x', FlashCardLimits.TextMaxLength + 1);

        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = tooLong, Answer = "a" }, 400);
        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = "q", Answer = tooLong }, 400);
    }

    [Fact]
    public async Task A_set_holds_at_most_the_card_limit()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var id = new FlashCardSetId(set.Id);
        var now = DateTimeOffset.UtcNow;

        await store.AppendAsync(
            id,
            [.. Enumerable.Range(0, FlashCardLimits.MaxCardsPerSet).Select(i =>
                (FlashCardSetEvent)new FlashCardAdded(id, new FlashCard(FlashCardId.New(), $"q{i}", "a"), new(guardianId), now))],
            CancellationToken.None);

        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = "one too many", Answer = "a" }, 400);
    }

    [Fact]
    public async Task An_unknown_or_deleted_set_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        await PostAsync(fixture, token, $"/flash-cards/sets/{Guid.CreateVersion7()}/cards", new { Question = "q", Answer = "a" }, 404);
        await PostAsync(fixture, token, $"/flash-cards/sets/{set.Id}/cards", new { Question = "q", Answer = "a" }, 404);
    }
}
