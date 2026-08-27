using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class CreateMealForGroupValidator : AbstractValidator<CreateMealForGroup>
{
    public CreateMealForGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
