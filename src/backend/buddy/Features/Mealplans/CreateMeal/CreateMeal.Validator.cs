using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class CreateMealValidator : AbstractValidator<CreateMeal>
{
    public CreateMealValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
