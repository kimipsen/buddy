using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class UpdateMealDetailsValidator : AbstractValidator<UpdateMealDetails>
{
    public UpdateMealDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
