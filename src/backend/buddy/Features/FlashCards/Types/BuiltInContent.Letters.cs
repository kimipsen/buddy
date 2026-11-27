namespace buddy.Features.FlashCards;

// Card content of the Letters sets (D8, D14): the Danish alphabet (29 letters, Æ Ø Å last). Level 1 shows the
// capital and level 2 the lowercase letter on the front; the back is an example word starting with that letter
// (text only, no audio). Key: letter:<char>. The registry is BuiltInSetDescriptors and is not touched here.
public static partial class BuiltInContent
{
    private static readonly (string Capital, string Word)[] Alphabet =
    [
        ("A", "abe"), ("B", "bil"), ("C", "cykel"), ("D", "dukke"), ("E", "elefant"),
        ("F", "fisk"), ("G", "giraf"), ("H", "hund"), ("I", "is"), ("J", "jord"),
        ("K", "kat"), ("L", "lampe"), ("M", "mus"), ("N", "næse"), ("O", "orm"),
        ("P", "pære"), ("Q", "quiz"), ("R", "regn"), ("S", "sol"), ("T", "tog"),
        ("U", "ugle"), ("V", "vand"), ("W", "wienerbrød"), ("X", "xylofon"), ("Y", "yoghurt"),
        ("Z", "zebra"), ("Æ", "æble"), ("Ø", "ørn"), ("Å", "ål"),
    ];

    public static IReadOnlyList<BuiltInCard> Letters(int level) => level switch
    {
        1 => [.. Alphabet.Select(a => new BuiltInCard($"letter:{a.Capital}", a.Capital, Capitalise(a.Word)))],
        2 => [.. Alphabet.Select(a => new BuiltInCard($"letter:{a.Capital.ToLowerInvariant()}", a.Capital.ToLowerInvariant(), a.Word))],
        _ => [],
    };

    private static string Capitalise(string word) => string.Concat(word[..1].ToUpperInvariant(), word[1..]);
}
