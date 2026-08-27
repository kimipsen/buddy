using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class RateMealValidator : AbstractValidator<RateMeal>
{
    public RateMealValidator()
    {
        RuleFor(x => x.Stars).InclusiveBetween(1, 5).WithMessage("Stars must be between 1 and 5.");
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}
