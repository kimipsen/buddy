using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards;

public sealed class MathDeckTests
{
    public static TheoryData<MathOperation, int> AllowedRules()
    {
        TheoryData<MathOperation, int> data = [];

        foreach (var operation in Enum.GetValues<MathOperation>())
        {
            for (var limit = 0; limit <= 101; limit++)
            {
                if (MathDeck.IsValidLimit(operation, limit))
                {
                    data.Add(operation, limit);
                }
            }
        }

        return data;
    }

    [Fact]
    public void The_allowed_limits_are_2_to_100_for_addition_and_subtraction_and_1_to_12_for_tables()
    {
        Assert.False(MathDeck.IsValidLimit(MathOperation.Addition, 1));
        Assert.True(MathDeck.IsValidLimit(MathOperation.Addition, 2));
        Assert.True(MathDeck.IsValidLimit(MathOperation.Subtraction, 100));
        Assert.False(MathDeck.IsValidLimit(MathOperation.Subtraction, 101));
        Assert.False(MathDeck.IsValidLimit(MathOperation.Multiplication, 0));
        Assert.True(MathDeck.IsValidLimit(MathOperation.Multiplication, 12));
        Assert.False(MathDeck.IsValidLimit(MathOperation.Division, 13));
    }

    [Theory]
    [MemberData(nameof(AllowedRules))]
    public void Subtraction_never_goes_below_zero_and_division_is_always_whole(MathOperation operation, int limit)
    {
        var cards = MathDeck.Cards(new MathRule(operation, limit)).ToArray();

        Assert.All(cards, card =>
        {
            Assert.True(int.Parse(card.Answer, System.Globalization.CultureInfo.InvariantCulture) >= 0);

            switch (operation)
            {
                case MathOperation.Subtraction:
                    Assert.True(card.Left >= card.Right);
                    break;
                case MathOperation.Division:
                    Assert.Equal(0, card.Left % card.Right);
                    Assert.Equal(card.Left, card.Right * int.Parse(card.Answer, System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        });
    }

    [Theory]
    [MemberData(nameof(AllowedRules))]
    public void Every_card_has_a_unique_deterministic_key_and_the_closed_form_count_matches(MathOperation operation, int limit)
    {
        var rule = new MathRule(operation, limit);
        var cards = MathDeck.Cards(rule).ToArray();

        Assert.Equal(cards.Length, cards.Select(c => c.Key).Distinct().Count());
        Assert.Equal(cards.Length, MathDeck.UniqueCount(rule));
        Assert.Equal(cards.Select(c => c.Key), MathDeck.Cards(rule).Select(c => c.Key));
    }

    [Theory]
    [MemberData(nameof(AllowedRules))]
    public void Every_generated_key_is_a_member_of_its_own_rule_and_parses_back_to_the_same_card(MathOperation operation, int limit)
    {
        var rule = new MathRule(operation, limit);

        foreach (var card in MathDeck.Cards(rule))
        {
            Assert.True(MathDeck.TryGetCard(rule, card.Key, out var parsed));
            Assert.Equal(card, parsed);
        }
    }

    [Fact]
    public void Keys_have_the_documented_shape()
    {
        Assert.Equal("add:4+5", MathDeck.Cards(new MathRule(MathOperation.Addition, 10)).Single(c => c.Left == 4 && c.Right == 5).Key);
        Assert.Equal("sub:9-4", MathDeck.Cards(new MathRule(MathOperation.Subtraction, 10)).Single(c => c.Left == 9 && c.Right == 4).Key);

        var times = MathDeck.Cards(new MathRule(MathOperation.Multiplication, 7)).Single(c => c.Right == 3);
        Assert.Equal(("mul:7x3", "7 x 3", "21"), (times.Key, times.Question, times.Answer));

        var division = MathDeck.Cards(new MathRule(MathOperation.Division, 7)).Single(c => c.Right == 7 && c.Left == 21);
        Assert.Equal(("div:21/7", "21 / 7", "3"), (division.Key, division.Question, division.Answer));
    }

    [Theory]
    [InlineData(MathOperation.Addition, 10, "add:6+5")]
    [InlineData(MathOperation.Addition, 10, "add:-1+5")]
    [InlineData(MathOperation.Addition, 10, "add:04+5")]
    [InlineData(MathOperation.Addition, 10, "add:4+5+1")]
    [InlineData(MathOperation.Addition, 10, "sub:5-4")]
    [InlineData(MathOperation.Subtraction, 10, "sub:4-5")]
    [InlineData(MathOperation.Subtraction, 10, "sub:11-1")]
    [InlineData(MathOperation.Multiplication, 7, "mul:8x3")]
    [InlineData(MathOperation.Multiplication, 7, "mul:7x11")]
    [InlineData(MathOperation.Multiplication, 7, "mul:7x0")]
    [InlineData(MathOperation.Division, 7, "div:22/7")]
    [InlineData(MathOperation.Division, 7, "div:21/3")]
    [InlineData(MathOperation.Division, 7, "div:77/7")]
    [InlineData(MathOperation.Division, 7, "")]
    [InlineData(MathOperation.Division, 7, "div:")]
    [InlineData(MathOperation.Division, 7, "nonsense")]
    public void A_key_outside_the_deck_is_not_a_member(MathOperation operation, int limit, string key)
    {
        Assert.False(MathDeck.TryGetCard(new MathRule(operation, limit), key, out _));
    }

    [Fact]
    public void A_null_key_is_not_a_member()
    {
        Assert.False(MathDeck.TryGetCard(new MathRule(MathOperation.Addition, 10), null, out _));
    }

    [Fact]
    public void Draw_returns_exactly_the_requested_number_of_cards_cycling_through_a_small_deck()
    {
        var rule = new MathRule(MathOperation.Multiplication, 7);

        var deck = MathDeck.Draw(rule, 25, new Random(1));

        Assert.Equal(25, deck.Count);
        Assert.All(deck, card => Assert.True(MathDeck.TryGetCard(rule, card.Key, out _)));
        // Every card of the 10-card pool appears before any repeats: the first 10 are all different.
        Assert.Equal(10, deck.Take(10).Select(c => c.Key).Distinct().Count());
        Assert.Equal(10, deck.Skip(10).Take(10).Select(c => c.Key).Distinct().Count());
    }

    [Theory]
    [InlineData(2, 50)]
    [InlineData(3, 40)]
    [InlineData(10, 200)]
    public void A_deck_never_repeats_a_card_back_to_back(int poolSize, int count)
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var deck = CardDeck.Draw(Enumerable.Range(0, poolSize).ToArray(), count, new Random(seed));

            Assert.Equal(count, deck.Count);

            for (var i = 1; i < deck.Count; i++)
            {
                Assert.NotEqual(deck[i - 1], deck[i]);
            }
        }
    }

    [Fact]
    public void A_pool_of_one_repeats_that_card()
    {
        Assert.Equal([5, 5, 5], CardDeck.Draw([5], 3, new Random(1)));
    }

    [Fact]
    public void The_same_seed_gives_the_same_deck()
    {
        var rule = new MathRule(MathOperation.Addition, 20);

        Assert.Equal(
            MathDeck.Draw(rule, 30, new Random(42)).Select(c => c.Key),
            MathDeck.Draw(rule, 30, new Random(42)).Select(c => c.Key));
    }

    [Fact]
    public void Drawing_from_an_empty_pool_throws()
    {
        Assert.Throws<ArgumentException>(() => CardDeck.Draw(Array.Empty<int>(), 3, new Random(1)));
    }

    [Fact]
    public void Equal_weights_use_exactly_the_uniform_draw_for_the_same_seed()
    {
        var pool = Enumerable.Range(0, 20).ToArray();

        Assert.Equal(
            CardDeck.Draw(pool, 50, new Random(42)),
            CardDeck.Draw(pool, 50, new Random(42), _ => 4));
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(2, 50)]
    [InlineData(3, 50)]
    [InlineData(10, 50)]
    [InlineData(50, 50)]
    public void Weighted_draw_returns_the_exact_count_without_adjacent_repeats(int poolSize, int count)
    {
        var pool = Enumerable.Range(0, poolSize).ToArray();
        var deck = CardDeck.Draw(pool, count, new Random(17), card => card % 3 + 1);

        Assert.Equal(count, deck.Count);

        if (poolSize > 1)
        {
            Assert.All(deck.Zip(deck.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        }
    }

    [Fact]
    public void Weighted_draw_is_deterministic_for_the_same_seed_and_weights()
    {
        var pool = Enumerable.Range(0, 20).ToArray();

        Assert.Equal(
            CardDeck.Draw(pool, 50, new Random(42), card => card + 1),
            CardDeck.Draw(pool, 50, new Random(42), card => card + 1));
    }

    [Fact]
    public void A_full_weighted_round_contains_every_card_once()
    {
        var pool = Enumerable.Range(0, 20).ToArray();

        var deck = CardDeck.Draw(pool, pool.Length, new Random(42), card => card + 1);

        Assert.Equal(pool.Order(), deck.Order());
    }

    [Fact]
    public void A_partial_second_round_repeats_the_heavy_cards()
    {
        var pool = Enumerable.Range(0, 10).ToArray();

        var deck = CardDeck.Draw(pool, 15, new Random(1), card => card < 5 ? 16 : 1);

        Assert.Equal(pool, deck.Distinct().Order());
        Assert.Equal(
            Enumerable.Range(0, 5),
            deck.GroupBy(card => card).Where(group => group.Count() == 2).Select(group => group.Key).Order());
    }

    [Fact]
    public void Higher_weight_cards_are_drawn_earlier_but_lower_weight_cards_still_appear()
    {
        var pool = Enumerable.Range(0, 20).ToArray();
        var random = new Random(42);
        var appearances = pool.ToDictionary(card => card, _ => 0);

        for (var i = 0; i < 3_000; i++)
        {
            foreach (var card in CardDeck.Draw(pool, 5, random, card => card == 0 ? 16 : 1))
            {
                appearances[card]++;
            }
        }

        var averageLightAppearances = appearances.Where(pair => pair.Key != 0).Average(pair => pair.Value);
        var heavyToLightRatio = appearances[0] / averageLightAppearances;

        // The final partial round samples without replacement, so the inclusion ratio is intentionally
        // below the raw 16:1 weight ratio. These wide bounds catch ignored or inverted weights without
        // making the fixed-seed statistical test brittle.
        Assert.InRange(heavyToLightRatio, 3.0, 6.0);
        Assert.All(appearances.Where(pair => pair.Key != 0), pair => Assert.True(pair.Value > 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_weight_below_one_throws(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CardDeck.Draw([1, 2, 3], 3, new Random(1), card => card == 2 ? weight : 1));
    }

    [Fact]
    public void Math_deck_accepts_a_weight_selector()
    {
        var rule = new MathRule(MathOperation.Multiplication, 7);

        var deck = MathDeck.Draw(rule, 5, new Random(42), card => card.Right == 10 ? 16 : 1);

        Assert.Equal(5, deck.Count);
        Assert.Contains(deck, card => card.Right == 10);
    }
}
