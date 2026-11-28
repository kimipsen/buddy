using Alba;

using buddy.Features.FlashCards;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.GetFlashCardDeck;

[Collection(BuddyApiCollection.Name)]
public sealed class GetFlashCardDeckTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const int Seed = 8675309;

    private readonly List<Guid> _children = [];
    private IAlbaHost _seededHost = null!;

    public async Task InitializeAsync()
    {
        _seededHost = await fixture.SharedHostAsync(
            nameof(GetFlashCardDeckTests),
            new Dictionary<string, string?>(),
            services => services.Replace(ServiceDescriptor.Transient(_ => new Random(Seed))));
    }

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
    public async Task A_struggled_with_math_card_is_most_frequent_and_every_card_still_appears()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateMathSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var cards = MathDeck.Cards(set.Math!).ToArray();
        var target = cards[0];
        await SeedWeightsAsync(child.Id, set, enabled.Tiers[0], WeightResults(cards, target.Key));

        var counts = await DrawCountsAsync(childToken, set.Id, enabled.Tiers[0].Id, 120);

        Assert.Equal(cards.Select(card => card.Key).Order(), counts.Keys.Order());
        Assert.Equal(target.Key, counts.MaxBy(entry => entry.Value).Key);
    }

    [Fact]
    public async Task Weights_are_isolated_between_two_children_using_the_same_set()
    {
        var (guardianToken, _, firstChild, firstToken) = await CreateFamilyAsync(fixture);
        var secondChild = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sam");
        var secondToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, secondChild);
        var set = await CreateMathSetAsync(fixture, guardianToken);
        var firstEnabled = await EnableAsync(fixture, guardianToken, firstChild.Id, set.Id);
        var secondEnabled = await EnableAsync(fixture, guardianToken, secondChild.Id, set.Id);
        var cards = MathDeck.Cards(set.Math!).ToArray();
        var target = cards[0];
        await SeedWeightsAsync(firstChild.Id, set, firstEnabled.Tiers[0], WeightResults(cards, target.Key));

        var firstCounts = await DrawCountsAsync(firstToken, set.Id, firstEnabled.Tiers[0].Id, 100);
        var secondCounts = await DrawCountsAsync(secondToken, set.Id, secondEnabled.Tiers[0].Id, 100);

        Assert.Equal(target.Key, firstCounts.MaxBy(entry => entry.Value).Key);
        Assert.True(firstCounts[target.Key] > secondCounts[target.Key] * 1.5,
            $"Expected only the child who missed {target.Key} to favour it; counts were {firstCounts[target.Key]} and {secondCounts[target.Key]}.");
    }

    [Fact]
    public async Task Editing_a_guardian_card_resets_only_that_cards_weight_and_new_results_use_the_new_text()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateTextSetAsync(fixture, guardianToken, "Planets");
        for (var index = 0; index < 12; index++)
        {
            set = await AddCardAsync(fixture, guardianToken, set.Id, $"Question {index}", $"Answer {index}");
        }

        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var target = set.Cards[0];
        var other = set.Cards[1];
        await SeedWeightsAsync(child.Id, set, enabled.Tiers[0],
        [
            .. Missed(target, 3),
            .. Missed(other, 3),
            .. set.Cards.Skip(2).Select((card, slot) => Result(card, slot + 6, knew: true))
        ]);

        set = (await PutAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}/cards/{target.Id}",
            new { Question = "Edited question", Answer = "Edited answer" })).ReadAsJson<FlashCardSetResponse>();
        var afterEdit = await DrawCountsAsync(childToken, set.Id, enabled.Tiers[0].Id, 120);

        Assert.True(afterEdit[other.Id.ToString()] > afterEdit[target.Id.ToString()] * 1.5,
            "The edited card should reset to the unseen weight while another card keeps its weight.");

        target = set.Cards.Single(card => card.Id == target.Id);
        await SeedWeightsAsync(child.Id, set, enabled.Tiers[0], Missed(target, 3));
        var afterNewGames = await DrawCountsAsync(childToken, set.Id, enabled.Tiers[0].Id, 120);

        Assert.True(afterNewGames[target.Id.ToString()] > afterEdit[target.Id.ToString()],
            "Later games should build weight from the edited card text.");
    }

    [Fact]
    public async Task A_retired_weight_key_is_ignored_and_the_built_in_deck_keeps_its_count()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, FixtureBuiltInCatalogue.ClockId.Value);
        var set = FixtureBuiltInCatalogue.Instance.TryFind(FixtureBuiltInCatalogue.ClockId)!;
        await SeedWeightsAsync(child.Id, FlashCardSetResponse.FromSet(set), enabled.Tiers[0],
        [
            new FlashCardResult(0, FixtureBuiltInCatalogue.RetiredKey, "Old", "Removed", false, 1)
        ]);

        var deck = await GetDeckAsync(fixture, childToken, set.Id.Value, enabled.Tiers[0].Id);

        Assert.Equal(enabled.Tiers[0].CardCount, deck.Cards.Count);
        Assert.DoesNotContain(deck.Cards, card => card.Key == FixtureBuiltInCatalogue.RetiredKey);
    }

    [Fact]
    public async Task A_child_with_no_finished_games_gets_the_existing_seeded_uniform_deck()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateMathSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var expected = MathDeck.Draw(set.Math!, enabled.Tiers[0].CardCount, new Random(Seed)).Select(card => card.Key);

        var deck = await GetDeckFromHostAsync(_seededHost, childToken, set.Id, enabled.Tiers[0].Id);

        Assert.Equal(expected, deck.Cards.Select(card => card.Key));
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

    private static async Task<FlashCardDeckResponse> GetDeckFromHostAsync(IAlbaHost host, string token, Guid setId, Guid tierId) =>
        (await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url(DeckUrl(setId, tierId));
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<FlashCardDeckResponse>();

    private async Task SeedWeightsAsync(
        Guid childId,
        FlashCardSetResponse set,
        FlashCardTierResponse tier,
        IEnumerable<FlashCardResult> results)
    {
        var id = FlashCardPracticeId.ForChild(new buddy.Features.Users.UserId(childId));
        var resultArray = results.ToArray();
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        await practices.AppendAsync(id,
        [
            new FlashCardGameFinished(
                id,
                Guid.CreateVersion7(),
                new FlashCardSetId(set.Id),
                set.Name,
                new FlashCardTierId(tier.Id),
                new DateOnly(2026, 1, 1),
                resultArray.Length,
                [.. resultArray],
                0,
                DateTimeOffset.UnixEpoch)
        ], CancellationToken.None);
    }

    private async Task<Dictionary<string, int>> DrawCountsAsync(string token, Guid setId, Guid tierId, int decks)
    {
        Dictionary<string, int> counts = [];
        for (var index = 0; index < decks; index++)
        {
            foreach (var card in (await GetDeckAsync(fixture, token, setId, tierId)).Cards)
            {
                counts[card.Key] = counts.GetValueOrDefault(card.Key) + 1;
            }
        }

        return counts;
    }

    private static FlashCardResult[] WeightResults(IEnumerable<MathCard> cards, string targetKey) =>
    [
        .. cards.Select((card, slot) => new FlashCardResult(slot, card.Key, card.Question, card.Answer, card.Key != targetKey, 1)),
        .. Enumerable.Range(0, 2).Select(index =>
        {
            var card = cards.Single(candidate => candidate.Key == targetKey);
            return new FlashCardResult(100 + index, card.Key, card.Question, card.Answer, false, 1);
        })
    ];

    private static IEnumerable<FlashCardResult> Missed(FlashCardResponse card, int count) =>
        Enumerable.Range(0, count).Select(index => Result(card, index, knew: false));

    private static FlashCardResult Result(FlashCardResponse card, int slot, bool knew) =>
        new(slot, card.Id.ToString(), card.Question, card.Answer, knew, 1);
}
