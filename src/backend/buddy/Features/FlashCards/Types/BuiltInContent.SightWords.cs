namespace buddy.Features.FlashCards;

// Card content of the SightWords sets (D7, D8, D14): the word is both the front and the back (no separate back),
// so history and the self-check flip still show something. Key: sight:<word> (lower-case, NFC).
// The registry is BuiltInSetDescriptors and is not touched here.
public static partial class BuiltInContent
{
    // Level 1, 0. klasse (12).
    private static readonly string[] SightWordsLevel1 =
        ["og", "er", "en", "et", "i", "jeg", "du", "vi", "på", "den", "det", "med"];

    // Level 2, 1. klasse (12).
    private static readonly string[] SightWordsLevel2 =
        ["har", "som", "til", "de", "for", "men", "kan", "var", "her", "ikke", "min", "mig"];

    // Level 3, 2. klasse (13).
    private static readonly string[] SightWordsLevel3 =
        ["efter", "ud", "op", "hvad", "hvor", "skal", "vil", "være", "fra", "hun", "ham", "nu", "når"];

    // Level 4, harder words (13).
    private static readonly string[] SightWordsLevel4 =
        ["sammen", "nogle", "mange", "altid", "aldrig", "også", "fordi", "igennem", "bagefter", "hvornår", "hvorfor", "meget", "noget"];

    public static IReadOnlyList<BuiltInCard> SightWords(int level) => level switch
    {
        1 => SightCards(SightWordsLevel1),
        2 => SightCards(SightWordsLevel2),
        3 => SightCards(SightWordsLevel3),
        4 => SightCards(SightWordsLevel4),
        _ => [],
    };

    private static List<BuiltInCard> SightCards(string[] words) =>
        [.. words.Select(w => new BuiltInCard($"sight:{w}", w, w))];
}
