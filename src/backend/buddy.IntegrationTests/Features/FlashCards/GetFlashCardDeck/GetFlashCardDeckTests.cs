using buddy.Features.FlashCards;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.GetFlashCardDeck;

[Collection(BuddyApiCollection.Name)]
public sealed class GetFlashCardDeckTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _children = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _children.ForEach(TestFlashCardPracticeEventStore.Disarm);

        return Task.CompletedTask;
    }

    private static string DeckUrl(Guid setId, Guid tierId) => $"/flash-cards/me/sets/{setId}/deck?tierId={tierId}";

    [Fact]
    [CoversEndpoint("GetFlashCardDeck")]
    public async Task A_deck_has_exactly_the_tiers_card_count_and_repeats_when_the_set_is_small()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);

        Assert.Equal(10, deck.Cards.Count);
        Assert.All(deck.Cards, card =>
        {
            Assert.Equal(set.Cards[0].Id.ToString(), card.Key);
            Assert.Equal("Capital of Denmark?", card.Question);
            Assert.Equal("Copenhagen", card.Answer);
            Assert.Null(card.Math);
        });
    }

    [Fact]
    public async Task A_math_deck_carries_operands_and_the_operator_next_to_the_rendered_question()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateMathSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        Assert.Equal(5, deck.Cards.Count);
        Assert.All(deck.Cards, card =>
        {
            var math = Assert.IsType<DeckMathResponse>(card.Math);
            Assert.Equal(MathOperation.Addition, math.Operation);
            Assert.Equal($"{math.Left} + {math.Right}", card.Question);
            Assert.Equal((math.Left + math.Right).ToString(System.Globalization.CultureInfo.InvariantCulture), card.Answer);
            Assert.True(math.Left + math.Right <= 10);
        });
    }

    [Fact]
    public async Task The_deck_response_carries_no_game_id()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var response = await GetAsync(fixture, childToken, DeckUrl(set.Id, enabled.Tiers[0].Id));

        Assert.DoesNotContain("gameId", response.ReadAsText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Loading_a_deck_persists_nothing()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var id = FlashCardPracticeId.ForChild(new buddy.Features.Users.UserId(child.Id));
        var before = await practices.ReadVersionAsync(id, CancellationToken.None);

        await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        Assert.Equal(before, await practices.ReadVersionAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task A_deck_never_reads_the_practice_stream()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        _children.Add(child.Id);
        TestFlashCardPracticeEventStore.Record(child.Id);

        await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        Assert.Equal([nameof(IFlashCardPracticeEventStore.FindSnapshotAsync)], TestFlashCardPracticeEventStore.Calls(child.Id));
    }

    [Fact]
    public async Task An_unknown_tier_a_disabled_set_and_a_deleted_set_are_not_found()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        await GetAsync(fixture, childToken, DeckUrl(set.Id, Guid.NewGuid()), 404);
        await GetAsync(fixture, childToken, DeckUrl(Guid.NewGuid(), enabled.Tiers[0].Id), 404);

        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}");
        await GetAsync(fixture, childToken, DeckUrl(set.Id, enabled.Tiers[0].Id), 404);

        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        var otherEnabled = await EnableAsync(fixture, guardianToken, child.Id, other.Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{other.Id}");
        await GetAsync(fixture, childToken, DeckUrl(other.Id, otherEnabled.Tiers[0].Id), 404);
    }

    [Fact]
    public async Task A_set_whose_cards_were_all_removed_cannot_be_played()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}");

        await GetAsync(fixture, childToken, DeckUrl(set.Id, enabled.Tiers[0].Id), 400);
    }

    [Fact]
    public async Task A_guardian_and_another_child_cannot_load_the_deck()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var (_, _, _, otherChildToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        await GetAsync(fixture, guardianToken, DeckUrl(set.Id, enabled.Tiers[0].Id), 404);
        await GetAsync(fixture, otherChildToken, DeckUrl(set.Id, enabled.Tiers[0].Id), 404);
    }
}
