using System.Collections.Immutable;

using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards;

public sealed class CardWeightingTests
{
    private static readonly FlashCardSetId SetId = FlashCardSetId.New();

    [Fact]
    public void Not_yet_raises_by_boost_and_stops_at_cap()
    {
        var weights = Apply([], Result("card", knewFirstTime: false));
        Assert.Equal(CardWeighting.BaseWeight + CardWeighting.Boost, Assert.Single(weights).Weight);

        weights = Apply(weights, Result("card", knewFirstTime: false));
        Assert.Equal(CardWeighting.BaseWeight + (2 * CardWeighting.Boost), Assert.Single(weights).Weight);

        weights = Apply(weights, Result("card", knewFirstTime: false));
        weights = Apply(weights, Result("card", knewFirstTime: false));
        Assert.Equal(CardWeighting.Cap, Assert.Single(weights).Weight);
    }

    [Fact]
    public void Knew_halves_the_weight_and_stops_at_floor_without_reaching_zero()
    {
        var hash = CardWeighting.TextHash("q", "a");
        ImmutableList<CardWeight> weights = [new(SetId, "card", CardWeighting.Cap, hash)];

        Assert.Equal(8, Assert.Single(weights = Apply(weights, Result("card", knewFirstTime: true))).Weight);
        Assert.Equal(4, Assert.Single(weights = Apply(weights, Result("card", knewFirstTime: true))).Weight);
        Assert.Equal(2, Assert.Single(weights = Apply(weights, Result("card", knewFirstTime: true))).Weight);
        Assert.Equal(CardWeighting.Floor, Assert.Single(weights = Apply(weights, Result("card", knewFirstTime: true))).Weight);
        Assert.Equal(CardWeighting.Floor, Assert.Single(Apply(weights, Result("card", knewFirstTime: true))).Weight);
    }

    [Fact]
    public void Not_yet_then_two_knew_results_produce_4_8_4_2()
    {
        Assert.Equal(CardWeighting.BaseWeight, CardWeighting.WeightOf(Practice([]), SetId)("card", "q", "a"));

        var weights = Apply([], Result("card", knewFirstTime: false));
        Assert.Equal(8, Assert.Single(weights).Weight);

        weights = Apply(weights, Result("card", knewFirstTime: true));
        Assert.Equal(4, Assert.Single(weights).Weight);

        weights = Apply(weights, Result("card", knewFirstTime: true));
        Assert.Equal(2, Assert.Single(weights).Weight);
    }

    [Fact]
    public void Changed_text_resets_to_base_weight_before_applying_the_result()
    {
        ImmutableList<CardWeight> weights =
        [
            new(SetId, "card", CardWeighting.Cap, CardWeighting.TextHash("old question", "old answer"))
        ];

        var changed = CardWeighting.Apply(weights, SetId, [new FlashCardResult(0, "card", "new question", "new answer", true, 1)]);

        Assert.Equal(CardWeighting.BaseWeight / 2, Assert.Single(changed).Weight);
        Assert.Equal(CardWeighting.TextHash("new question", "new answer"), Assert.Single(changed).TextHash);
    }

    [Fact]
    public void The_same_card_in_two_slots_is_folded_twice_in_slot_order()
    {
        var weights = CardWeighting.Apply(
            [],
            SetId,
            [
                new FlashCardResult(1, "card", "q", "a", true, 1),
                new FlashCardResult(0, "card", "q", "a", false, 2)
            ]);

        Assert.Equal(4, Assert.Single(weights).Weight);
    }

    [Fact]
    public void Pruning_drops_lowest_weights_then_ordinal_card_key_and_is_deterministic()
    {
        var hash = CardWeighting.TextHash("q", "a");
        var otherSet = FlashCardSetId.New();
        ImmutableList<CardWeight> initial =
        [
            .. Enumerable.Range(0, CardWeighting.MaxTrackedCardsPerSet)
                .Select(index => new CardWeight(SetId, $"card-{index:D3}", index == 0 ? CardWeighting.Floor : CardWeighting.BaseWeight, hash)),
            new(otherSet, "keep", CardWeighting.Floor, hash)
        ];
        var result = new FlashCardResult(0, "new-card", "q", "a", false, 2);

        var first = CardWeighting.Apply(initial, SetId, [result]);
        var second = CardWeighting.Apply(initial, SetId, [result]);

        Assert.Equal(CardWeighting.MaxTrackedCardsPerSet, first.Count(weight => weight.SetId == SetId));
        Assert.DoesNotContain(first, weight => weight.SetId == SetId && weight.CardKey == "card-000");
        Assert.Contains(first, weight => weight.SetId == SetId && weight.CardKey == "new-card");
        Assert.Contains(first, weight => weight.SetId == otherSet && weight.CardKey == "keep");
        Assert.Equal(first, second);

        var tied = initial.SetItem(0, initial[0] with { Weight = CardWeighting.BaseWeight });
        var tiedResult = CardWeighting.Apply(tied, SetId, [result]);
        Assert.DoesNotContain(tiedResult, weight => weight.SetId == SetId && weight.CardKey == "card-000");
    }

    [Fact]
    public void Weight_lookup_uses_base_for_unknown_or_changed_text_and_clamps_stored_boundaries()
    {
        var hash = CardWeighting.TextHash("q", "a");
        var practice = Practice(
        [
            new(SetId, "below", CardWeighting.Floor - 10, hash),
            new(SetId, "above", CardWeighting.Cap + 10, hash),
            new(SetId, "changed", CardWeighting.Cap, hash)
        ]);
        var weightOf = CardWeighting.WeightOf(practice, SetId);

        Assert.Equal(CardWeighting.BaseWeight, weightOf("unknown", "q", "a"));
        Assert.Equal(CardWeighting.BaseWeight, weightOf("changed", "different", "a"));
        Assert.Equal(CardWeighting.Floor, weightOf("below", "q", "a"));
        Assert.Equal(CardWeighting.Cap, weightOf("above", "q", "a"));
    }

    [Fact]
    public void Text_hash_is_stable_lower_case_hex_and_keeps_question_answer_boundaries()
    {
        var hash = CardWeighting.TextHash("q", "a");

        Assert.Equal(16, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(hash, CardWeighting.TextHash("q", "a"));
        Assert.NotEqual(CardWeighting.TextHash("qa", ""), CardWeighting.TextHash("q", "a"));
    }

    private static ImmutableList<CardWeight> Apply(ImmutableList<CardWeight> weights, FlashCardResult result) =>
        CardWeighting.Apply(weights, SetId, [result]);

    private static FlashCardResult Result(string key, bool knewFirstTime) => new(0, key, "q", "a", knewFirstTime, knewFirstTime ? 1 : 2);

    private static FlashCardPractice Practice(ImmutableList<CardWeight> weights)
    {
        var child = new buddy.Features.Users.UserId(Guid.CreateVersion7());
        return new FlashCardPractice(FlashCardPracticeId.ForChild(child), child, [], [], weights);
    }
}
