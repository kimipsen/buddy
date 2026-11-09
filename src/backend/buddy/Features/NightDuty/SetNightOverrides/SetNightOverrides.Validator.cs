using FluentValidation;

namespace buddy.Features.NightDuty;

public sealed class SetNightOverridesValidator : AbstractValidator<SetNightOverrides>
{
    public SetNightOverridesValidator()
    {
        RuleFor(x => x.Nights)
            .NotEmpty()
            .WithMessage("nights must contain at least one night.")
            .Must(nights => nights.Count <= NightDutyRules.MaxRangeDays)
            .WithMessage($"At most {NightDutyRules.MaxRangeDays} nights can be set at once.")
            .Must(nights => nights.Select(n => n.Date).Distinct().Count() == nights.Count)
            .WithMessage("Each date can appear only once.")
            // The response resolves every night between the first and last date, so the span is
            // capped like a list range.
            .Must(nights => nights.Count == 0
                || nights.Max(n => n.Date).DayNumber - nights.Min(n => n.Date).DayNumber <= NightDutyRules.MaxRangeDays)
            .WithMessage($"The nights must lie within {NightDutyRules.MaxRangeDays} days of each other.")
            .OverridePropertyName("nights");

        RuleForEach(x => x.Nights)
            .ChildRules(night => night.RuleFor(n => n.Override.PlannedEntry!)
                .SetValidator(new NightEntryValidator())
                .OverridePropertyName("entry")
                .When(n => n.Override.PlannedEntry is not null))
            .OverridePropertyName("nights");
    }
}
