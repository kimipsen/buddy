namespace buddy.Features.TaskLibrary;

// Fixed at creation. A routine lays its steps out back to back from the scheduled start; a
// checklist's items (a packing list) are all due at the scheduled "leave by" time and have no
// duration. See docs/backend/analysis/packing-checklists.md, Question 2.
public enum TaskTemplateKind
{
    Routine = 0,
    Checklist = 1,
}
