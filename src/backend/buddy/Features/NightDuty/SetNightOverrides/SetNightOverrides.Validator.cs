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
            .OverridePropertyName("nights");

        RuleForEach(x => x.Nights)
            .ChildRules(night => night.RuleFor(n => n.Override.PlannedEntry!)
                .SetValidator(new NightEntryValidator())
                .OverridePropertyName("entry")
                .When(n => n.Override.PlannedEntry is not null))
            .OverridePropertyName("nights");
    }
}
