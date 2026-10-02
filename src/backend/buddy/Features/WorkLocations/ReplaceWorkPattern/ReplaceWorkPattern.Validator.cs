using FluentValidation;

namespace buddy.Features.WorkLocations;

// Structural rules only. "Every LocationId is one of this guardian's active locations" needs the
// aggregate and stays in ReplaceWorkPatternHandler.
public sealed class ReplaceWorkPatternValidator : AbstractValidator<ReplaceWorkPattern>
{
    public ReplaceWorkPatternValidator()
    {
        RuleFor(x => x.Pattern.CycleWeeks)
            .InclusiveBetween(1, WorkPattern.MaxCycleWeeks)
            .OverridePropertyName("cycleWeeks");

        RuleFor(x => x.Pattern.AnchorMonday)
            .Must(date => date.DayOfWeek == DayOfWeek.Monday)
            .WithMessage("anchorMonday must be a Monday.")
            .OverridePropertyName("anchorMonday");

        RuleForEach(x => x.Pattern.Days)
            .Must((command, day) => day.Week >= 0 && day.Week < command.Pattern.CycleWeeks)
            .WithMessage("Each day's week must be between 0 and cycleWeeks - 1.")
            .Must(day => Enum.IsDefined(day.Day))
            .WithMessage("Each day must be a valid weekday.")
            .OverridePropertyName("days");

        RuleFor(x => x.Pattern.Days)
            .Must(days => days.Select(d => (d.Week, d.Day)).Distinct().Count() == days.Count)
            .WithMessage("A pattern can have at most one location per week and weekday.")
            .OverridePropertyName("days");
    }
}
