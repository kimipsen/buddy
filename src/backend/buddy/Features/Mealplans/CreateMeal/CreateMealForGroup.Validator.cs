using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class CreateMealForGroupValidator : AbstractValidator<CreateMealForGroup>
{
    public CreateMealForGroupValidator()
    {
        this.ValidMealDetails(x => x.Name, x => x.Description, x => x.Icon, x => x.Color, x => x.EatingOut);
    }
}
