using System.Globalization;
using System.Text;

using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The real sight-word, letter and English content, plain unit tests on BuiltInCatalogue.Default (no Alba host),
// so the fixture catalogue of the integration host never hides a hole in the shipped content.
public sealed class SightLettersEnglishContentTests
{
    private static IReadOnlyList<BuiltInCard> Cards(string setKey) =>
        BuiltInSetDescriptors.All.Single(d => d.Key == setKey).Cards;

    private static IEnumerable<BuiltInCard> Sight => new[] { 1, 2, 3, 4 }.SelectMany(l => Cards($"sight:{l}"));

    private static IEnumerable<BuiltInCard> Letters => new[] { 1, 2 }.SelectMany(l => Cards($"letters:{l}"));

    private static IEnumerable<BuiltInCard> English => new[] { 1, 2, 3 }.SelectMany(l => Cards($"english:{l}"));

    [Theory]
    [InlineData("sight:1", 12)]
    [InlineData("sight:2", 12)]
    [InlineData("sight:3", 13)]
    [InlineData("sight:4", 13)]
    [InlineData("letters:1", 29)]
    [InlineData("letters:2", 29)]
    [InlineData("english:1", 17)]
    [InlineData("english:2", 17)]
    [InlineData("english:3", 16)]
    public void Each_level_has_the_stated_number_of_unique_cards(string setKey, int expected)
    {
        var set = BuiltInCatalogue.Default.All.Single(s => s.BuiltIn!.Key == setKey);

        Assert.Equal(expected, Cards(setKey).Count);
        Assert.Equal(expected, set.UniqueCardCount);
        Assert.Equal(expected, Cards(setKey).Select(c => c.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("sight:1")]
    [InlineData("sight:2")]
    [InlineData("sight:3")]
    [InlineData("sight:4")]
    [InlineData("letters:1")]
    [InlineData("letters:2")]
    [InlineData("english:1")]
    [InlineData("english:2")]
    [InlineData("english:3")]
    public void Each_set_has_cards_and_a_non_null_example_card_on_the_catalogue(string setKey)
    {
        var set = BuiltInCatalogue.Default.All.Single(s => s.BuiltIn!.Key == setKey);

        Assert.True(set.UniqueCardCount >= 1);
        Assert.NotNull(set.BuiltIn!.Example);
        Assert.Equal(Cards(setKey)[0].Key, set.BuiltIn.Example.Key);
    }

    [Fact]
    public void Sight_words_are_about_50_with_the_word_as_both_sides()
    {
        var all = Sight.ToArray();

        Assert.Equal(50, all.Length);
        Assert.Equal(50, all.Select(c => c.Key).Distinct().Count());
        Assert.All(all, c =>
        {
            Assert.Equal($"sight:{c.Question}", c.Key);
            Assert.Equal(c.Question, c.Answer);
            Assert.Equal(c.Question.ToLower(new CultureInfo("da-DK")), c.Question);
            Assert.Null(c.Clock);
        });
    }

    [Fact]
    public void Letters_are_the_29_danish_capitals_then_the_29_lowercase_letters()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ";

        Assert.Equal(alphabet.Select(c => c.ToString()), Cards("letters:1").Select(c => c.Question));
        Assert.Equal(alphabet.ToLowerInvariant().Select(c => c.ToString()), Cards("letters:2").Select(c => c.Question));
        Assert.Equal(29, alphabet.Length);
    }

    [Fact]
    public void Every_letter_card_has_a_letter_key_and_an_example_word_starting_with_that_letter()
    {
        Assert.All(Letters, c =>
        {
            Assert.Equal($"letter:{c.Question}", c.Key);
            Assert.StartsWith(c.Question, c.Answer, StringComparison.OrdinalIgnoreCase);
            Assert.True(c.Answer.Length > 1 || c.Question is "I");
            Assert.Null(c.Clock);
        });

        Assert.Equal("Æble", Cards("letters:1").Single(c => c.Question == "Æ").Answer);
        Assert.Equal("ørn", Cards("letters:2").Single(c => c.Question == "ø").Answer);
        Assert.Equal("ål", Cards("letters:2").Single(c => c.Question == "å").Answer);
    }

    [Fact]
    public void Letter_cards_show_the_same_example_word_in_capital_and_lowercase_levels()
    {
        var capitals = Cards("letters:1").Select(c => c.Answer.ToLowerInvariant());
        var lower = Cards("letters:2").Select(c => c.Answer);

        Assert.Equal(capitals, lower);
        Assert.Equal(58, Letters.Select(c => c.Key).Distinct().Count());
    }

    [Fact]
    public void English_cards_go_from_danish_to_english()
    {
        var all = English.ToArray();

        Assert.Equal(50, all.Length);
        Assert.Equal(50, all.Select(c => c.Key).Distinct().Count());
        Assert.Equal(50, all.Select(c => c.Question).Distinct().Count());
        Assert.All(all, c =>
        {
            Assert.Equal($"en:{c.Question}", c.Key);
            Assert.NotEqual(c.Question, c.Answer);
            Assert.Matches("^[a-z]+$", c.Answer);
            Assert.Null(c.Clock);
        });
        Assert.Equal("dog", Cards("english:1").Single(c => c.Key == "en:hund").Answer);
    }

    [Fact]
    public void All_text_is_nfc_normalised_trimmed_and_has_no_empty_strings()
    {
        Assert.All(Sight.Concat(Letters).Concat(English), c =>
        {
            foreach (var text in new[] { c.Key, c.Question, c.Answer })
            {
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.Equal(text.Trim(), text);
                Assert.True(text.IsNormalized(NormalizationForm.FormC));
            }
        });
    }

    [Fact]
    public void Keys_are_unique_across_the_sight_letter_and_english_sets()
    {
        var keys = Sight.Concat(Letters).Concat(English).Select(c => c.Key).ToArray();

        Assert.Equal(158, keys.Length);
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }
}
