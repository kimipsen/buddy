using FluentValidation;

namespace buddy.Features.TransitionWarnings;

public sealed class ConfigureTransitionWarningsValidator : AbstractValidator<ConfigureTransitionWarnings>
{
    public ConfigureTransitionWarningsValidator()
    {
        // An empty list isn't "off": the switch says that, so the form has one way to turn warnings off.
        RuleFor(x => x.LeadMinutes).ValidLeadMinutes(allowEmpty: false);

        RuleFor(x => x.Kinds)
            .Must(kinds => kinds.Length > 0).When(x => x.Enabled).WithMessage("Choose at least one kind of item to warn about.");
        RuleForEach(x => x.Kinds).IsInEnum();

        RuleFor(x => x.QuietHours)
            .Must(quiet => quiet is null || quiet.From != quiet.To).WithMessage("Quiet hours must start and end at different times.");
    }
}
