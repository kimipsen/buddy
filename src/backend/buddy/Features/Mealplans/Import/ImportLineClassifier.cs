using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace buddy.Features.Mealplans;

// Turns one day's free text ("Spaghetti m. ostepølser + GS 🍝") into a kind, a clean meal name
// ("Spaghetti m. ostepølser"), notes ("+ GS") and a matching key ("spaghetti m ostepølser").
// Shared by every import format -- see docs/backend/analysis/mealplan-import.md, Questions 3 and 4.
public static partial class ImportLineClassifier
{
    public const int MaxMealNameLength = 200;
    public const int MaxNotesLength = 2000;

    public sealed record Classified(ImportLineKind Kind, string MealName, string Notes, string Key);

    // Whole words or phrases (of the normalized key) that mean nobody ate dinner at home. Tuned to
    // Danish family notes; "i <Place>" and a leading "-" are handled separately below.
    private static readonly string[] AwayMarkers =
    [
        "ingen hjemme", "sommerhus", "bedstefar", "bedstemor", "farmor", "farfar",
        "mormor", "morfar", "juleaften", "nytårsaften", "ferie", "nobody home",
    ];

    // Only at the start: "Ikke hjemme" is an away day, "Nachos (Mor ikke hjemme)" is a dinner.
    private static readonly string[] AwayPrefixes = ["ikke hjemme", "not home", "spise hos", "spiser hos", "eating out"];

    private static readonly string[] LeftoverWords = ["rester", "leftovers"];

    // null when nothing is left after removing emojis and notes: an empty day.
    public static Classified? Classify(string rawText)
    {
        // "m/ laks", "u/ søskende": Danish shorthand for "med"/"uden", not an alternative.
        var text = Shorthand().Replace(StripSymbols(rawText), "$1. ");
        var notes = new List<string>();

        // "(Mor ikke hjemme)" -> notes.
        text = Parenthesized().Replace(text, match =>
        {
            notes.Add(match.Groups[1].Value.Trim());
            return " ";
        });

        // "... + GS", "... +kyllingeostebrød" -> notes, from the first '+'.
        var plus = text.IndexOf('+');

        if (plus >= 0)
        {
            notes.Insert(0, Collapse(text[plus..]));
            text = text[..plus];
        }

        // "Tomatsuppe -> Pizza": the plan changed; the dish after the last arrow is what was eaten.
        var arrow = Arrow().Matches(text).LastOrDefault();

        if (arrow is not null)
        {
            notes.Insert(0, Collapse(text));
            text = text[(arrow.Index + arrow.Length)..];
        }

        // "Nachos - Sally ikke hjemme" -> notes, from the first spaced dash after a name.
        var dash = text.IndexOf(" - ", StringComparison.Ordinal);

        if (dash > 0 && text[..dash].Trim().Trim('-').Length > 0)
        {
            notes.Add(Collapse(text[(dash + 3)..]));
            text = text[..dash];
        }

        var name = TrimName(text);

        if (name.Length == 0)
        {
            return null;
        }

        // Whole words only: "Mormors frikadeller" and "Feriepizza" are dinners.
        var words = $" {NormalizeKey(name)} ";
        var kind = ImportLineKind.Meal;

        if (rawText.TrimStart().StartsWith('-')
            || AwayMarkers.Any(marker => words.Contains($" {marker} ", StringComparison.Ordinal))
            || AwayPrefixes.Any(prefix => words.StartsWith($" {prefix} ", StringComparison.Ordinal))
            || AwayPlace().IsMatch(name))
        {
            kind = ImportLineKind.Away;
        }
        else
        {
            var options = AlternativeSeparator().Split(name)
                .Select(TrimName)
                .Where(option => option.Length > 0)
                .ToList();
            var meals = options.Where(option => !IsLeftovers(option)).ToList();

            if (meals.Count == 0)
            {
                kind = ImportLineKind.Leftovers;
            }
            else if (options.Count > 1)
            {
                kind = ImportLineKind.Alternatives;
                notes.Insert(0, name);
                name = meals[0];
            }
        }

        name = Capitalize(name);

        if (name.Length > MaxMealNameLength)
        {
            notes.Insert(0, name);
            var cut = name.LastIndexOf(' ', MaxMealNameLength);
            name = name[..(cut > 0 ? cut : MaxMealNameLength)];
        }

        var joinedNotes = string.Join(" ", notes.Where(n => n.Length > 0));

        if (joinedNotes.Length > MaxNotesLength)
        {
            joinedNotes = joinedNotes[..MaxNotesLength];
        }

        return new Classified(kind, name, joinedNotes, NormalizeKey(name));
    }

    // Lower-case, punctuation removed, "m"/"m."/"med" folded to "m", whitespace collapsed, so
    // "Rugbrød m fisk 🐟", "rugbrød med fisk" and "Rugbrød m. fisk" share one key.
    public static string NormalizeKey(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (var c in StripSymbols(name).ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word == "med" ? "m" : word);

        return string.Join(' ', words);
    }

    private static bool IsLeftovers(string option)
    {
        var words = NormalizeKey(option).Split(' ');

        return words.Any(word => LeftoverWords.Contains(word));
    }

    // Emojis, pictographs, variation selectors and joiners -- everything that isn't a letter,
    // digit, punctuation or space. Letters stay intact: NFC first so "ø" isn't a base letter
    // plus a combining mark.
    private static string StripSymbols(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            var category = char.GetUnicodeCategory(c);

            var keep = category switch
            {
                UnicodeCategory.OtherSymbol or UnicodeCategory.Surrogate or UnicodeCategory.ModifierSymbol
                    or UnicodeCategory.Format or UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
                    or UnicodeCategory.PrivateUse or UnicodeCategory.Control => false,
                _ => true,
            };

            builder.Append(keep ? c : ' ');
        }

        return builder.ToString();
    }

    private static string TrimName(string text) => Collapse(text).Trim(' ', '-', '.', ',', '?', '!', ':', ';', '/');

    private static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    private static string Capitalize(string name) =>
        name.Length == 0 ? name : char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];

    [GeneratedRegex(@"\(([^)]*)\)?")]
    private static partial Regex Parenthesized();

    // A slash with whitespace on at least one side: "Sushi / McD", "rester /" -- but not
    // "ris/blomkålsris", which is one dish.
    [GeneratedRegex(@"\s+/\s*|\s*/\s+")]
    private static partial Regex AlternativeSeparator();

    // "i Silkeborg": in a (capitalized) place.
    [GeneratedRegex(@"^i\s+\p{Lu}")]
    private static partial Regex AwayPlace();

    [GeneratedRegex(@"\b([mMuU])/\s*")]
    private static partial Regex Shorthand();

    [GeneratedRegex(@"->|→|=>")]
    private static partial Regex Arrow();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
