using FluentValidation;

namespace buddy.Features.HouseRules;

public sealed class EditRuleValidator : AbstractValidator<EditRule>
{
    public EditRuleValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(RuleContentRules.MaxTitleLength);
        RuleFor(x => x.Body).MaximumLength(RuleContentRules.MaxBodyLength);
    }
}
