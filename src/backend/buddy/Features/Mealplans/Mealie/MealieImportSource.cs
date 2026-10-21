namespace buddy.Features.Mealplans;

// Turns a Mealie meal plan into the ParsedImport the text formats produce, so the rest of the
// import (review, commit, never overwrite, revert) is the same code. Not an IMealPlanImportFormat:
// that interface parses pasted text; this reads entries fetched over the network for a date range.
// See docs/backend/analysis/mealie-integration.md, Question 5.
public static class MealieImportSource
{
    public const string FormatId = "mealie";

    public const string EntryMergedWarning = "mealie_entry_merged";
    public const string EntrySkippedWarning = "mealie_entry_skipped";
    public const string DuplicateSlotWarning = "duplicate_slot";
    public const string RangeEmptyWarning = "mealie_range_empty";

    public sealed record Result(ParsedImport Parsed, IReadOnlyDictionary<string, MealieRecipeRef> RecipesByKey);

    private sealed record Entry(int LineNumber, MealiePlanEntryDto Dto, string Name, MealieRecipeRef? Recipe);

    public static Result Build(IReadOnlyList<MealiePlanEntryDto> entries, IReadOnlyCollection<Meal> familyMeals)
    {
        List<ImportWarning> warnings = [];

        // Recipe id -> the library meal already linked to it (an active one before an archived one).
        var linkedMeals = familyMeals
            .Where(m => m.Recipe is not null)
            .OrderBy(m => m.IsArchived)
            .GroupBy(m => m.Recipe!.RecipeId)
            .ToDictionary(g => g.Key, g => g.First());

        // Line numbers follow the review's order: by date, then Mealie's own entry type order.
        var ordered = entries
            .OrderBy(e => e.Date)
            .ThenBy(e => EntryTypeOrder(e.EntryType))
            .ThenBy(e => e.Id)
            .Select((dto, index) => new Entry(index + 1, dto, NameOf(dto), RecipeOf(dto)))
            .ToList();

        foreach (var nameless in ordered.Where(e => e.Name.Length == 0))
        {
            warnings.Add(new ImportWarning(nameless.LineNumber, EntrySkippedWarning, "A Mealie entry with neither a recipe nor a title was skipped."));
        }

        var named = ordered.Where(e => e.Name.Length > 0).ToList();
        List<ParsedImportLine> lines = [];
        Dictionary<string, MealieRecipeRef> recipesByKey = [];

        foreach (var day in named.GroupBy(e => e.Dto.Date))
        {
            var extras = day.Where(e => SlotOf(e.Dto.EntryType) is null).ToList();
            var slotted = day.Where(e => SlotOf(e.Dto.EntryType) is not null).GroupBy(e => SlotOf(e.Dto.EntryType)!.Value).ToList();

            foreach (var slot in slotted)
            {
                var first = slot.First();
                List<string> notes = [first.Dto.Text?.Trim() ?? ""];

                // A slot holds one meal: the first entry is it, the others go in its notes.
                foreach (var other in slot.Skip(1))
                {
                    notes.Add("+ " + other.Name);
                    warnings.Add(new ImportWarning(other.LineNumber, DuplicateSlotWarning,
                        $"{first.Dto.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} has more than one {slot.Key} in Mealie; \"{other.Name}\" was added to the notes of \"{first.Name}\"."));
                }

                // Side dishes, drinks and desserts have no slot in Buddy: they go in that day's
                // dinner notes ("+ side: Green salad").
                if (slot.Key == MealSlot.Dinner)
                {
                    foreach (var extra in extras)
                    {
                        notes.Add($"+ {extra.Dto.EntryType}: {extra.Name}");
                        warnings.Add(new ImportWarning(extra.LineNumber, EntryMergedWarning,
                            $"\"{extra.Name}\" ({extra.Dto.EntryType}) was added to the notes of that day's dinner."));
                    }

                    extras.Clear();
                }

                var key = first.Recipe is { } recipe && linkedMeals.TryGetValue(recipe.RecipeId, out var linked)
                    // Matching by recipe first: the linked meal's own key is an exact match in the review.
                    ? ImportLineClassifier.NormalizeKey(linked.Name)
                    : ImportLineClassifier.NormalizeKey(first.Name);

                if (first.Recipe is { } firstRecipe)
                {
                    recipesByKey.TryAdd(key, firstRecipe);
                }

                lines.Add(new ParsedImportLine(
                    first.LineNumber,
                    first.Dto.Date,
                    slot.Key,
                    first.Name,
                    ImportLineKind.Meal,
                    Truncate(first.Name, ImportLineClassifier.MaxMealNameLength),
                    Truncate(string.Join(" ", notes.Where(n => n.Length > 0)), ImportLineClassifier.MaxNotesLength),
                    key));
            }

            foreach (var extra in extras)
            {
                warnings.Add(new ImportWarning(extra.LineNumber, EntrySkippedWarning,
                    $"\"{extra.Name}\" ({extra.Dto.EntryType}) was skipped: there is no dinner that day to add it to."));
            }
        }

        if (entries.Count == 0)
        {
            warnings.Add(new ImportWarning(0, RangeEmptyWarning, "Mealie has no meal plan entries in this date range."));
        }

        return new Result(
            new ParsedImport([.. lines.OrderBy(l => l.LineNumber)], [.. warnings.OrderBy(w => w.LineNumber)], 0),
            recipesByKey);
    }

    // Mealie's PlanEntryType values are lower-case on the wire.
    public static MealSlot? SlotOf(string? entryType) => entryType switch
    {
        "breakfast" => MealSlot.Breakfast,
        "lunch" => MealSlot.Lunch,
        "dinner" => MealSlot.Dinner,
        "snack" => MealSlot.Snack,
        _ => null,
    };

    private static int EntryTypeOrder(string? entryType) => entryType switch
    {
        "breakfast" => 0,
        "lunch" => 1,
        "dinner" => 2,
        "side" => 3,
        "snack" => 4,
        "drink" => 5,
        "dessert" => 6,
        _ => 7,
    };

    private static string NameOf(MealiePlanEntryDto dto) =>
        (dto.Recipe?.Name ?? "").Trim() is { Length: > 0 } recipeName ? recipeName : (dto.Title ?? "").Trim();

    private static MealieRecipeRef? RecipeOf(MealiePlanEntryDto dto) =>
        (dto.RecipeId ?? dto.Recipe?.Id) is { } id && id != Guid.Empty
            ? new MealieRecipeRef(id, dto.Recipe?.Slug ?? "", dto.Recipe?.Name ?? dto.Title ?? "")
            : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd();
}
