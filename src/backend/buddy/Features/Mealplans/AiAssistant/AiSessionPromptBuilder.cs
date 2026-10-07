using System.Globalization;
using System.Text;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Builds the system prompt fresh on every turn from the family's current meal library/ratings --
// this is the "stable context" a provider adapter can mark as a cache breakpoint (Anthropic) since
// it repeats near-verbatim across a session's turns (see the AI mealplan plan's caching note).
public static class AiSessionPromptBuilder
{
    public static string Build(MealplanAiSession session, IReadOnlyCollection<Meal> familyMeals, IReadOnlyCollection<MealId> mustIncludeMealIds, string notes)
    {
        var builder = new StringBuilder();

        builder.AppendLine("You are a meal-planning assistant helping a parent or guardian fill in their family's mealplan.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Requested date range: {session.From:yyyy-MM-dd} to {session.To:yyyy-MM-dd}.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Requested meal slots: {string.Join(", ", session.RequestedSlots)}.");

        if (mustIncludeMealIds.Count > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"The guardian asked to make sure these meal ids appear somewhere in the plan: {string.Join(", ", mustIncludeMealIds.Select(id => id.Value))}.");
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Guardian's notes: {notes}");
        }

        builder.AppendLine();
        builder.AppendLine("Available meals -- only propose meal ids from this list:");

        var activeMeals = familyMeals.Where(m => !m.IsArchived).ToList();
        var childAliases = BuildChildAliases(activeMeals);

        foreach (var meal in activeMeals)
        {
            var ratings = meal.Ratings.Count == 0
                ? "no ratings yet"
                : string.Join("; ", meal.Ratings.OrderBy(r => childAliases[r.Key]).Select(r =>
                    $"child {childAliases[r.Key]}: {r.Value.Stars}/5{(string.IsNullOrWhiteSpace(r.Value.Comment) ? "" : $" (\"{r.Value.Comment}\")")}"));

            builder.AppendLine(CultureInfo.InvariantCulture, $"- id={meal.Id.Value} name=\"{meal.Name}\" ratings: {ratings}");
        }

        builder.AppendLine();
        builder.AppendLine(
            "Use the propose_assignment tool to fill a slot and clear_draft_assignment to remove a prior pick. " +
            "Before proposing meals for a date you haven't checked yet, call get_calendar_conflicts for that date " +
            "range so you can flag things like dining out or a trip instead of silently proposing a meal for that " +
            "day -- ask the guardian whether to skip it rather than deciding yourself. " +
            "Favor meals the children have rated highly, and vary meals across the plan rather than repeating the " +
            "same one every day unless asked to. After making any tool calls for this message, always reply with a " +
            "short, friendly summary of what you picked (or a clarifying question) -- never end a turn with only " +
            "tool calls and no reply.");

        return builder.ToString();
    }

    // Children are "child 1", "child 2" in the prompt, never their UserId (GDPR Question 6 in
    // docs/backend/analysis/gdpr-data-protection.md). Numbered in UserId order, so the numbering
    // stays the same across a session's turns unless a new child rates a meal mid-session.
    private static Dictionary<UserId, int> BuildChildAliases(IEnumerable<Meal> meals) =>
        meals
            .SelectMany(m => m.Ratings.Keys)
            .Distinct()
            .Order(Comparer<UserId>.Create((a, b) => a.Value.CompareTo(b.Value)))
            .Select((childId, index) => (childId, index))
            .ToDictionary(x => x.childId, x => x.index + 1);
}
