using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class UpdateMealDetailsForGroupValidator : AbstractValidator<UpdateMealDetailsForGroup>
{
    public UpdateMealDetailsForGroupValidator()
    {
        this.ValidMealDetails(x => x.Name, x => x.Description, x => x.Icon, x => x.Color, x => x.EatingOut);
    }
}
