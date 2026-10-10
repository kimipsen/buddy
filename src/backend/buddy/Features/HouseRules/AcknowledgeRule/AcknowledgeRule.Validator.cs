using FluentValidation;

namespace buddy.Features.HouseRules;

public sealed class AcknowledgeRuleValidator : AbstractValidator<AcknowledgeRule>
{
    public AcknowledgeRuleValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(1);
    }
}
