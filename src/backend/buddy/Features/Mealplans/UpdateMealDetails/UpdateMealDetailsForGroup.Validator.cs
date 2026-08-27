using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class UpdateMealDetailsForGroupValidator : AbstractValidator<UpdateMealDetailsForGroup>
{
    public UpdateMealDetailsForGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
