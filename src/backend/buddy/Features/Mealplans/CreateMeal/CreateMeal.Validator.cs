using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class CreateMealValidator : AbstractValidator<CreateMeal>
{
    public CreateMealValidator()
    {
        this.ValidMealDetails(x => x.Name, x => x.Description, x => x.Icon, x => x.Color, x => x.EatingOut);
    }
}
