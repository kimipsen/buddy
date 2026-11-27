using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The golden card keys of the sight-word, letter and English sets (D8), plain unit tests on BuiltInSetDescriptors.
// A key is a contract: results, history and a stale client refer to it. A key that disappears from a set
// fails here; a deliberate removal moves the key to BuiltInCatalogue.BuiltInRetiredKeys and out of this list.
public sealed class SightLettersEnglishKeysAreStable
{
    private static readonly string[] Sight1 =
    [
        "sight:og", "sight:er", "sight:en", "sight:et", "sight:i",
        "sight:jeg", "sight:du", "sight:vi", "sight:på", "sight:den",
        "sight:det", "sight:med"
    ];

    private static readonly string[] Sight2 =
    [
        "sight:har", "sight:som", "sight:til", "sight:de", "sight:for",
        "sight:men", "sight:kan", "sight:var", "sight:her", "sight:ikke",
        "sight:min", "sight:mig"
    ];

    private static readonly string[] Sight3 =
    [
        "sight:efter", "sight:ud", "sight:op", "sight:hvad", "sight:hvor",
        "sight:skal", "sight:vil", "sight:være", "sight:fra", "sight:hun",
        "sight:ham", "sight:nu", "sight:når"
    ];

    private static readonly string[] Sight4 =
    [
        "sight:sammen", "sight:nogle", "sight:mange", "sight:altid", "sight:aldrig",
        "sight:også", "sight:fordi", "sight:igennem", "sight:bagefter", "sight:hvornår",
        "sight:hvorfor", "sight:meget", "sight:noget"
    ];

    private static readonly string[] Letters1 =
    [
        "letter:A", "letter:B", "letter:C", "letter:D", "letter:E",
        "letter:F", "letter:G", "letter:H", "letter:I", "letter:J",
        "letter:K", "letter:L", "letter:M", "letter:N", "letter:O",
        "letter:P", "letter:Q", "letter:R", "letter:S", "letter:T",
        "letter:U", "letter:V", "letter:W", "letter:X", "letter:Y",
        "letter:Z", "letter:Æ", "letter:Ø", "letter:Å"
    ];

    private static readonly string[] Letters2 =
    [
        "letter:a", "letter:b", "letter:c", "letter:d", "letter:e",
        "letter:f", "letter:g", "letter:h", "letter:i", "letter:j",
        "letter:k", "letter:l", "letter:m", "letter:n", "letter:o",
        "letter:p", "letter:q", "letter:r", "letter:s", "letter:t",
        "letter:u", "letter:v", "letter:w", "letter:x", "letter:y",
        "letter:z", "letter:æ", "letter:ø", "letter:å"
    ];

    private static readonly string[] English1 =
    [
        "en:hund", "en:kat", "en:hus", "en:bil", "en:bog",
        "en:sol", "en:måne", "en:vand", "en:mad", "en:ven",
        "en:bold", "en:træ", "en:fisk", "en:æble", "en:mælk",
        "en:brød", "en:skole"
    ];

    private static readonly string[] English2 =
    [
        "en:lærer", "en:stol", "en:bord", "en:vindue", "en:dør",
        "en:seng", "en:hest", "en:ko", "en:gris", "en:fugl",
        "en:blomst", "en:regn", "en:sne", "en:rød", "en:blå",
        "en:grøn", "en:gul"
    ];

    private static readonly string[] English3 =
    [
        "en:morgen", "en:aften", "en:nat", "en:uge", "en:måned",
        "en:år", "en:cykel", "en:flyvemaskine", "en:sommer", "en:vinter",
        "en:bjerg", "en:hav", "en:kage", "en:legetøj", "en:glad",
        "en:trist"
    ];

    public static TheoryData<string, string[]> Golden => new()
    {
        { "sight:1", Sight1 },
        { "sight:2", Sight2 },
        { "sight:3", Sight3 },
        { "sight:4", Sight4 },
        { "letters:1", Letters1 },
        { "letters:2", Letters2 },
        { "english:1", English1 },
        { "english:2", English2 },
        { "english:3", English3 }
    };

    [Theory]
    [MemberData(nameof(Golden))]
    public void BuiltInKeysAreStable(string setKey, string[] expected)
    {
        var actual = BuiltInSetDescriptors.All.Single(d => d.Key == setKey).Cards.Select(c => c.Key).ToArray();

        Assert.Equal(expected, actual);
    }
}
