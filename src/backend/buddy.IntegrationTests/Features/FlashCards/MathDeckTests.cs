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
}
