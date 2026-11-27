using System.Globalization;
using System.Text;

using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The real clock and countries content, plain unit tests on BuiltInCatalogue.Default (no Alba host), so the
// fixture catalogue of the integration host never hides a hole in the shipped content.
public sealed class ClockAndCountriesContentTests
{
    private static IReadOnlyList<BuiltInCard> Cards(string setKey) =>
        BuiltInSetDescriptors.All.Single(d => d.Key == setKey).Cards;

    private static IEnumerable<BuiltInCard> Clock => new[] { 1, 2, 3, 4 }.SelectMany(l => Cards($"clock:{l}"));

    private static IEnumerable<BuiltInCard> Countries => new[] { 1, 2, 3 }.SelectMany(l => Cards($"countries:{l}"));

    [Theory]
    [InlineData("clock:1", 12)]
    [InlineData("clock:2", 12)]
    [InlineData("clock:3", 24)]
    [InlineData("clock:4", 96)]
    [InlineData("countries:1", 15)]
    [InlineData("countries:2", 35)]
    [InlineData("countries:3", 50)]
    public void Each_level_has_the_stated_number_of_unique_cards(string setKey, int expected)
    {
        var set = BuiltInCatalogue.Default.All.Single(s => s.BuiltIn!.Key == setKey);

        Assert.Equal(expected, Cards(setKey).Count);
        Assert.Equal(expected, set.UniqueCardCount);
        Assert.Equal(expected, Cards(setKey).Select(c => c.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("clock:1")]
    [InlineData("clock:2")]
    [InlineData("clock:3")]
    [InlineData("clock:4")]
    [InlineData("countries:1")]
    [InlineData("countries:2")]
    [InlineData("countries:3")]
    public void Each_set_has_cards_and_a_non_null_example_card_on_the_catalogue(string setKey)
    {
        var set = BuiltInCatalogue.Default.All.Single(s => s.BuiltIn!.Key == setKey);

        Assert.True(set.UniqueCardCount >= 1);
        Assert.NotNull(set.BuiltIn!.Example);
        Assert.Equal(Cards(setKey)[0].Key, set.BuiltIn.Example.Key);
    }

    [Fact]
    public void The_clock_levels_are_disjoint_and_total_144()
    {
        var keys = Clock.Select(c => c.Key).ToArray();

        Assert.Equal(144, keys.Length);
        Assert.Equal(144, keys.Distinct().Count());
        Assert.Equal(144, Clock.Select(c => (c.Clock!.Hour, c.Clock.Minute)).Distinct().Count());
        Assert.All(Clock, c => Assert.Equal($"clock:{DanishClock.Digital(c.Clock!.Hour, c.Clock.Minute)}", c.Key));
    }

    [Fact]
    public void The_clock_levels_hold_the_planned_minutes()
    {
        Assert.All(Cards("clock:1"), c => Assert.Equal(0, c.Clock!.Minute));
        Assert.All(Cards("clock:2"), c => Assert.Equal(30, c.Clock!.Minute));
        Assert.All(Cards("clock:3"), c => Assert.Contains(c.Clock!.Minute, new[] { 15, 45 }));
        Assert.All(Cards("clock:4"), c => Assert.DoesNotContain(c.Clock!.Minute, new[] { 0, 15, 30, 45 }));
        Assert.Equal(Enumerable.Range(1, 12), Cards("clock:1").Select(c => c.Clock!.Hour));
    }

    [Fact]
    public void Every_clock_question_is_the_neutral_prompt_and_never_contains_its_time_words()
    {
        Assert.All(Clock, c =>
        {
            Assert.Equal("Hvad er klokken?", c.Question);
            Assert.DoesNotContain(DanishClock.Words(c.Clock!.Hour, c.Clock.Minute), c.Question);
            Assert.DoesNotContain(DanishClock.Digital(c.Clock.Hour, c.Clock.Minute), c.Question);
        });
    }

    [Fact]
    public void Every_clock_answer_is_the_danish_words_and_the_digital_time()
    {
        Assert.All(Clock, c => Assert.Equal($"{DanishClock.Words(c.Clock!.Hour, c.Clock.Minute)} ({DanishClock.Digital(c.Clock.Hour, c.Clock.Minute)})", c.Answer));
        Assert.Equal("halv fire (3:30)", Clock.Single(c => c.Key == "clock:3:30").Answer);
        Assert.Equal("kvart i fire (3:45)", Clock.Single(c => c.Key == "clock:3:45").Answer);
        Assert.Equal(144, Clock.Select(c => c.Answer).Distinct().Count());
    }

    [Fact]
    public void Every_country_has_a_canonical_key_a_two_indicator_flag_and_a_danish_name()
    {
        Assert.All(Countries, c =>
        {
            Assert.Matches("^flag:[A-Z]{2}$", c.Key);
            Assert.Null(c.Clock);

            var iso = c.Key["flag:".Length..];
            Assert.Equal(BuiltInContent.FlagEmoji(iso), c.Question);

            var codePoints = c.Question.EnumerateRunes().Select(r => r.Value).ToArray();
            Assert.Equal(2, codePoints.Length);
            Assert.All(codePoints, cp => Assert.InRange(cp, 0x1F1E6, 0x1F1FF));
            Assert.Equal(iso, string.Concat(codePoints.Select(cp => (char)('A' + cp - 0x1F1E6))));

            Assert.False(string.IsNullOrWhiteSpace(c.Answer));
            Assert.Equal(c.Answer.Trim(), c.Answer);
        });
    }

    [Fact]
    public void The_country_levels_are_disjoint_and_names_are_unique()
    {
        var all = Countries.ToArray();

        Assert.Equal(100, all.Length);
        Assert.Equal(100, all.Select(c => c.Key).Distinct().Count());
        Assert.Equal(100, all.Select(c => c.Answer).Distinct().Count());
    }

    [Fact]
    public void Flag_emoji_are_built_from_regional_indicators()
    {
        Assert.Equal("\U0001F1E9\U0001F1EA", BuiltInContent.FlagEmoji("DE"));
        Assert.Equal("Tyskland", Countries.Single(c => c.Key == "flag:DE").Answer);
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInContent.FlagEmoji("DEU"));
    }

    [Fact]
    public void All_text_is_nfc_normalised_and_has_no_empty_strings()
    {
        Assert.All(Clock.Concat(Countries), c =>
        {
            foreach (var text in new[] { c.Key, c.Question, c.Answer })
            {
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.True(text.IsNormalized(NormalizationForm.FormC));
            }
        });
    }

    [Fact]
    public void Keys_are_unique_across_the_clock_and_country_sets()
    {
        var keys = Clock.Concat(Countries).Select(c => c.Key).ToArray();

        Assert.Equal(244, keys.Length);
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(keys, k => Assert.Equal(k, k.ToString(CultureInfo.InvariantCulture)));
    }
}
