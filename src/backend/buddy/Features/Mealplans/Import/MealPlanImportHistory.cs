using System.Collections.Immutable;

namespace buddy.Features.Mealplans;

public sealed record MealPlanImportSummary(
    MealPlanImportId ImportId,
    string Format,
    DateOnly From,
    DateOnly To,
    int EntryCount,
    int CreatedMealCount,
    Guid ImportedBy,
    DateTimeOffset ImportedAt,
    bool Reverted);

// Imports are folded straight from the plan's events rather than kept on the MealPlan aggregate:
// adding a field there would change the shape of every stored MealPlanSnapshot, and only these
// two rarely-used slices ever need it.
public static class MealPlanImportHistory
{
    public static IReadOnlyList<MealPlanImportSummary> Summarize(IEnumerable<MealPlanEvent> events)
    {
        var imports = new List<MealPlanImportSummary>();
        var reverted = new HashSet<MealPlanImportId>();

        foreach (var @event in events)
        {
            switch (@event)
            {
                case MealPlanEntriesImported imported when imported.Entries.Length > 0:
                    imports.Add(new MealPlanImportSummary(
                        imported.ImportId,
                        imported.Format,
                        imported.Entries.Min(e => e.Date),
                        imported.Entries.Max(e => e.Date),
                        imported.Entries.Length,
                        imported.CreatedMealIds.Length,
                        imported.ImportedBy.Value,
                        imported.OccurredAt,
                        Reverted: false));
                    break;

                case MealPlanImportReverted revert:
                    reverted.Add(revert.ImportId);
                    break;
            }
        }

        return [.. imports
            .Select(i => i with { Reverted = reverted.Contains(i.ImportId) })
            .OrderByDescending(i => i.ImportedAt)];
    }

    public static MealPlanEntriesImported? Find(IEnumerable<MealPlanEvent> events, MealPlanImportId importId) =>
        events.Select(e => e.Value).OfType<MealPlanEntriesImported>().FirstOrDefault(e => e.ImportId == importId);

    public static bool IsReverted(IEnumerable<MealPlanEvent> events, MealPlanImportId importId) =>
        events.Select(e => e.Value).OfType<MealPlanImportReverted>().Any(e => e.ImportId == importId);

    // The import's entries whose slot was last written by this import -- not by a guardian since,
    // and not by a later import that happened to write the same meal again. Value equality on the
    // assignment would also clear that later import's days.
    public static IReadOnlyList<ImportedMealPlanEntry> EntriesStillFrom(IEnumerable<MealPlanEvent> events, MealPlanImportId importId)
    {
        var lastWriter = new Dictionary<(DateOnly, MealSlot), MealPlanImportId?>();
        MealPlanEntriesImported? target = null;

        foreach (var @event in events)
        {
            switch (@event)
            {
                case MealAssignedToSlot assigned:
                    lastWriter[(assigned.Date, assigned.Slot)] = null;
                    break;

                case MealSlotCleared cleared:
                    lastWriter.Remove((cleared.Date, cleared.Slot));
                    break;

                case MealPlanEntriesImported imported:
                    target = imported.ImportId == importId ? imported : target;

                    foreach (var entry in imported.Entries)
                    {
                        lastWriter[(entry.Date, entry.Slot)] = imported.ImportId;
                    }

                    break;
            }
        }

        return target is null
            ? []
            : [.. target.Entries.Where(e => lastWriter.GetValueOrDefault((e.Date, e.Slot)) == importId)];
    }

    // Meal ids any slot of the plan still points at.
    public static ImmutableHashSet<MealId> MealsInUse(MealPlan plan) =>
        [.. plan.Assignments.Values.Select(a => a.MealId)];
}
