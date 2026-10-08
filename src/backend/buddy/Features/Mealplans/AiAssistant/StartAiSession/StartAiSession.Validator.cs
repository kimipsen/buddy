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
        RuleFor(x => x.ServedWithin).IsInEnum().WithMessage("Served within must be 0, 30, 60 or 90 days.");
    }
}
