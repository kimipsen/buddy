using buddy.Common.Validation;

namespace buddy.Features.TaskLibrary;

// A routine step needs a duration (its steps are laid out back to back); a checklist item has none
// (every item is due at the "leave by" time). It depends on the loaded template's kind, so
// AddSubtask/UpdateSubtask check it in the handler rather than the validator
// (docs/backend/analysis/packing-checklists.md, Question 2).
internal static class SubtaskDurationRule
{
    public static ValidationProblem? Check(TaskTemplateKind kind, TimeSpan duration) => kind switch
    {
        TaskTemplateKind.Routine when duration == TimeSpan.Zero => OnDuration("A routine step needs a duration."),
        TaskTemplateKind.Checklist when duration != TimeSpan.Zero => OnDuration("A checklist item has no duration."),
        _ => null,
    };

    // Keyed on the field, like the validator rule it replaced, so a form can show it next to the input.
    private static ValidationProblem OnDuration(string message) =>
        new(new Dictionary<string, string[]> { [nameof(AddSubtask.Duration)] = [message] });
}
