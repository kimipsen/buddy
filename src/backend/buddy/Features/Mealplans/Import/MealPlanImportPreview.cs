using System.Collections.Immutable;

namespace buddy.Features.Mealplans;

public enum ImportGroupAction
{
    // Assign the matched existing meal.
    Existing,
    // Create one new meal for the group.
    New,
    // Import nothing for these days.
    Skip
}

public sealed record MealPlanImportPreviewLine(
    int LineNumber,
    DateOnly Date,
    MealSlot Slot,
    string RawText,
    ImportLineKind Kind,
    string MealName,
    string Notes,
    string Key,
    bool Occupied);

// One row of the review: every line sharing a normalized key. Matched* is an exact key match in
// the family's library (an active meal wins over an archived one); Suggested* is only a "Did you
// mean ...?" hint -- an existing meal or a more frequent group -- and is never applied by itself.
public sealed record MealPlanImportPreviewGroup(
    string Key,
    string Name,
    ImportLineKind Kind,
    int Count,
    DateOnly FirstDate,
    DateOnly LastDate,
    ImportGroupAction DefaultAction,
    MealId? MatchedMealId,
    string MatchedMealName,
    MealId? SuggestedMealId,
    string SuggestedGroupKey,
    string SuggestedName);

public sealed record MealPlanImportPreview(
    string Format,
    IReadOnlyList<MealPlanImportPreviewLine> Lines,
    IReadOnlyList<MealPlanImportPreviewGroup> Groups,
    IReadOnlyList<ImportWarning> Warnings,
    int EmptyDays);

// Builds the review draft from a parse, the family's meal library and the current plan.
// Pure -- see docs/backend/analysis/mealplan-import.md, Question 3.
public static class MealPlanImportPreviewBuilder
{
    // Similarity (1 - edit distance / length) at or above which a name is suggested as a match.
    private const double SuggestionThreshold = 0.85;

    public static MealPlanImportPreview Build(
        string format,
        ParsedImport parsed,
        IReadOnlyCollection<Meal> familyMeals,
        ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment> assignments)
    {
        var lines = parsed.Lines
            .Select(l => new MealPlanImportPreviewLine(
                l.LineNumber, l.Date, l.Slot, l.RawText, l.Kind, l.MealName, l.Notes, l.Key,
                assignments.ContainsKey((l.Date, l.Slot))))
            .ToList();

        // Active before archived, so an exact key match prefers a meal that can still be picked.
        var mealsByKey = familyMeals
            .OrderBy(m => m.IsArchived)
            .GroupBy(m => ImportLineClassifier.NormalizeKey(m.Name))
            .ToDictionary(g => g.Key, g => g.First());

        var drafts = lines
            .GroupBy(l => l.Key)
            .Select(g => new Draft(
                g.Key,
                g.First().MealName,
                g.Any(l => l.Kind is ImportLineKind.Meal) ? ImportLineKind.Meal : g.First().Kind,
                g.Count(),
                g.Min(l => l.Date),
                g.Max(l => l.Date),
                mealsByKey.GetValueOrDefault(g.Key)))
            .OrderByDescending(d => d.Count)
            .ThenBy(d => d.Key, StringComparer.Ordinal)
            .ToList();

        var groups = drafts.Select(draft => ToGroup(draft, drafts, mealsByKey)).ToList();

        return new MealPlanImportPreview(format, lines, groups, parsed.Warnings, parsed.EmptyDays);
    }

    private sealed record Draft(string Key, string Name, ImportLineKind Kind, int Count, DateOnly FirstDate, DateOnly LastDate, Meal? Match);

    private static MealPlanImportPreviewGroup ToGroup(Draft draft, IReadOnlyList<Draft> all, IReadOnlyDictionary<string, Meal> mealsByKey)
    {
        var action = draft switch
        {
            { Kind: ImportLineKind.Away or ImportLineKind.Leftovers } => ImportGroupAction.Skip,
            { Match: not null } => ImportGroupAction.Existing,
            _ => ImportGroupAction.New,
        };

        if (draft.Match is not null || action == ImportGroupAction.Skip)
        {
            return Group(draft, action, suggestedMeal: null, suggestedGroup: null);
        }

        var bestMeal = mealsByKey
            .Select(pair => (Meal: pair.Value, Score: Similarity(draft.Key, pair.Key)))
            .Where(c => c.Score >= SuggestionThreshold)
            .OrderByDescending(c => c.Score)
            .Select(c => c.Meal)
            .FirstOrDefault();

        if (bestMeal is not null)
        {
            return Group(draft, action, bestMeal, suggestedGroup: null);
        }

        // Only point at a group that is more frequent (ties: the alphabetically earlier key), so
        // two spellings never suggest each other.
        var bestGroup = all
            .Where(other => other.Key != draft.Key
                && other.Kind is ImportLineKind.Meal or ImportLineKind.Alternatives
                && (other.Count > draft.Count || (other.Count == draft.Count && string.CompareOrdinal(other.Key, draft.Key) < 0)))
            .Select(other => (Group: other, Score: Similarity(draft.Key, other.Key)))
            .Where(c => c.Score >= SuggestionThreshold)
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Group.Count)
            .Select(c => c.Group)
            .FirstOrDefault();

        return Group(draft, action, suggestedMeal: null, bestGroup);
    }

    private static MealPlanImportPreviewGroup Group(Draft draft, ImportGroupAction action, Meal? suggestedMeal, Draft? suggestedGroup) => new(
        draft.Key,
        draft.Name,
        draft.Kind,
        draft.Count,
        draft.FirstDate,
        draft.LastDate,
        action,
        draft.Match?.Id,
        draft.Match?.Name ?? "",
        suggestedMeal?.Id,
        suggestedGroup?.Key ?? "",
        suggestedMeal?.Name ?? suggestedGroup?.Name ?? "");

    // 1 for a singular/plural pair ("hotdog"/"hotdogs"), otherwise 1 - Levenshtein / longer length.
    // Short keys never match fuzzily: "pasta" vs "pizza" is a different dinner.
    internal static double Similarity(string a, string b)
    {
        if (a == b)
        {
            return 1;
        }

        if (IsPluralPair(a, b) || IsPluralPair(b, a))
        {
            return 1;
        }

        var longer = Math.Max(a.Length, b.Length);

        if (Math.Min(a.Length, b.Length) < 6 || Math.Abs(a.Length - b.Length) > longer * (1 - SuggestionThreshold))
        {
            return 0;
        }

        return 1 - (double)Levenshtein(a, b) / longer;
    }

    private static bool IsPluralPair(string singular, string plural) =>
        plural.Length > singular.Length && plural.StartsWith(singular, StringComparison.Ordinal)
        && plural[singular.Length..] is "s" or "e" or "er" or "r";

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
