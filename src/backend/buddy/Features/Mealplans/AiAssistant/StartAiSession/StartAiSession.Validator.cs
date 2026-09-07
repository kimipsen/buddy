using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class StartAiSessionValidator : AbstractValidator<StartAiSession>
{
    public const int MaxRangeDays = 31;

    public StartAiSessionValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, MaxRangeDays);

        RuleFor(x => x.RequestedSlots).NotEmpty().WithMessage("At least one meal slot must be requested.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
