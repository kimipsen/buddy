using FluentValidation;

namespace buddy.Features.NightDuty;

// Structural rules only. "EffectiveFrom is not in the past" needs the child's time zone, and the
// guardian/babysitter checks need other aggregates -- both stay in ReplaceNightPatternHandler.
public sealed class ReplaceNightPatternValidator : AbstractValidator<ReplaceNightPattern>
{
    public ReplaceNightPatternValidator()
    {
        RuleFor(x => x.CycleWeeks)
            .InclusiveBetween(1, NightPattern.MaxCycleWeeks)
            .OverridePropertyName("cycleWeeks");

        RuleFor(x => x.AnchorMonday)
            .Must(date => date.DayOfWeek == DayOfWeek.Monday)
            .WithMessage("anchorMonday must be a Monday.")
            .OverridePropertyName("anchorMonday");

        RuleForEach(x => x.Days)
            .Must((command, day) => day.Week >= 0 && day.Week < command.CycleWeeks)
            .WithMessage("Each day's week must be between 0 and cycleWeeks - 1.")
            .Must(day => Enum.IsDefined(day.Day))
            .WithMessage("Each day must be a valid weekday.")
            .OverridePropertyName("days");

        RuleForEach(x => x.Days)
            .ChildRules(day => day.RuleFor(d => d.Entry).SetValidator(new NightEntryValidator()).OverridePropertyName("entry"))
            .OverridePropertyName("days");

        RuleFor(x => x.Days)
            .Must(days => days.Select(d => (d.Week, d.Day)).Distinct().Count() == days.Count)
            .WithMessage("A schedule can have at most one entry per week and weekday.")
            .OverridePropertyName("days");
    }
}
