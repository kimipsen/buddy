namespace buddy.Features.TaskLibrary;

// A checklist's display options. EveningBeforeFrom (local time) is when the child home starts
// showing tomorrow's list; null shows it on the day only. See
// docs/backend/analysis/packing-checklists.md, Question 4.
public sealed record ChecklistOptions(TimeOnly? EveningBeforeFrom)
{
    public static readonly ChecklistOptions None = new(EveningBeforeFrom: null);
}
