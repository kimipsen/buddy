using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// Catalogue-wide totals over the shipped content (D8, D14): plain unit tests on BuiltInCatalogue.Default.
public sealed class BuiltInCatalogueTotalsTests
{
    private static readonly IReadOnlyList<FlashCardSet> Sets = BuiltInCatalogue.Default.All;

    private static IEnumerable<BuiltInCard> AllCards => BuiltInSetDescriptors.All.SelectMany(d => d.Cards);

    [Fact]
    public void The_catalogue_has_16_sets_and_about_400_entries()
    {
        Assert.Equal(16, Sets.Count);
        Assert.Equal(402, Sets.Sum(s => s.UniqueCardCount));
        Assert.InRange(AllCards.Count(), 380, 420);
    }

    [Fact]
    public void The_clock_sets_hold_144_cards()
    {
        Assert.Equal(144, Sets.Where(s => s.BuiltIn!.Key.StartsWith("clock:", StringComparison.Ordinal)).Sum(s => s.UniqueCardCount));
        Assert.Equal(144, AllCards.Count(c => c.Clock is not null));
    }

    [Fact]
    public void No_card_key_is_duplicated_within_or_across_sets()
    {
        var keys = AllCards.Select(c => c.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void Every_set_has_cards_and_an_example_card()
    {
        Assert.All(Sets, s =>
        {
            Assert.True(s.UniqueCardCount >= 1);
            Assert.NotNull(s.BuiltIn!.Example);
        });
    }
}
