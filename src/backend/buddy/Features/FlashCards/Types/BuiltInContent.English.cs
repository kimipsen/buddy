namespace buddy.Features.FlashCards;

// Card content of the English sets (D8, D14): a Danish word on the front, the English word on the back.
// Key: en:<danish>. The registry is BuiltInSetDescriptors and is not touched here.
public static partial class BuiltInContent
{
    // Level 1: everyday things (17).
    private static readonly (string Danish, string English)[] EnglishLevel1 =
    [
        ("hund", "dog"), ("kat", "cat"), ("hus", "house"), ("bil", "car"), ("bog", "book"),
        ("sol", "sun"), ("måne", "moon"), ("vand", "water"), ("mad", "food"), ("ven", "friend"),
        ("bold", "ball"), ("træ", "tree"), ("fisk", "fish"), ("æble", "apple"), ("mælk", "milk"),
        ("brød", "bread"), ("skole", "school"),
    ];

    // Level 2: school, home, animals, colours (17).
    private static readonly (string Danish, string English)[] EnglishLevel2 =
    [
        ("lærer", "teacher"), ("stol", "chair"), ("bord", "table"), ("vindue", "window"), ("dør", "door"),
        ("seng", "bed"), ("hest", "horse"), ("ko", "cow"), ("gris", "pig"), ("fugl", "bird"),
        ("blomst", "flower"), ("regn", "rain"), ("sne", "snow"), ("rød", "red"), ("blå", "blue"),
        ("grøn", "green"), ("gul", "yellow"),
    ];

    // Level 3: time, nature and feelings (16).
    private static readonly (string Danish, string English)[] EnglishLevel3 =
    [
        ("morgen", "morning"), ("aften", "evening"), ("nat", "night"), ("uge", "week"), ("måned", "month"),
        ("år", "year"), ("cykel", "bicycle"), ("flyvemaskine", "airplane"), ("sommer", "summer"), ("vinter", "winter"),
        ("bjerg", "mountain"), ("hav", "sea"), ("kage", "cake"), ("legetøj", "toy"), ("glad", "happy"),
        ("trist", "sad"),
    ];

    public static IReadOnlyList<BuiltInCard> English(int level) => level switch
    {
        1 => EnglishCards(EnglishLevel1),
        2 => EnglishCards(EnglishLevel2),
        3 => EnglishCards(EnglishLevel3),
        _ => [],
    };

    private static List<BuiltInCard> EnglishCards((string Danish, string English)[] words) =>
        [.. words.Select(w => new BuiltInCard($"en:{w.Danish}", w.Danish, w.English))];
}
