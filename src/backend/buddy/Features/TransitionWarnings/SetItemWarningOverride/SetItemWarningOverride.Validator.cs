using FluentValidation;

namespace buddy.Features.TransitionWarnings;

public sealed class SetItemWarningOverrideValidator : AbstractValidator<SetItemWarningOverride>
{
    public SetItemWarningOverrideValidator()
    {
        RuleFor(x => x.LeadMinutes).ValidLeadMinutes(allowEmpty: true);
    }
}
