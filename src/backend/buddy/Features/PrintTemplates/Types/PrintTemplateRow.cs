using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.Features.WorkLocations;

namespace buddy.Features.PrintTemplates;

// One printed row. Only the fields its Kind uses are set; everything else is null/false -- the
// validator rejects stray values (see PrintTemplateRowRules), so a stored row always says exactly
// what it means. See docs/backend/analysis/week-plan-print-templates.md, Question 3.
public sealed record PrintTemplateRow(
    PrintRowKind Kind,
    string Label,
    int HeightWeight,
    UserId? ChildId = null,
    GroupId? MealGroupId = null,
    MealSlot? MealSlot = null,
    UserId? GuardianId = null,
    WorkLocationId? WorkLocationId = null,
    IReadOnlyList<CalendarId>? CalendarIds = null,
    UserId? AssignedToId = null,
    string? TitleFilter = null,
    int? MaxItems = null,
    bool ShowTime = false,
    bool ShowAssignee = false)
{
    // Records compare IReadOnlyList members by reference, so content equality is spelled out.
    public bool IsSameAs(PrintTemplateRow other) =>
        this with { CalendarIds = null } == other with { CalendarIds = null }
        && (CalendarIds ?? []).SequenceEqual(other.CalendarIds ?? []);

    public static bool AreSame(IReadOnlyList<PrintTemplateRow> first, IReadOnlyList<PrintTemplateRow> second) =>
        first.Count == second.Count && first.Zip(second).All(pair => pair.First.IsSameAs(pair.Second));
}

// Colors a guardian's name in Pickup/WorkLocation/CalendarEvents cells. A list of small records
// rather than a dictionary keyed by a strongly-typed id, which serializes unpredictably.
public sealed record GuardianColor(UserId GuardianId, Color Color);
