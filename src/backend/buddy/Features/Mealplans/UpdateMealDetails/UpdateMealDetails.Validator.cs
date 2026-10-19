using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class UpdateMealDetailsValidator : AbstractValidator<UpdateMealDetails>
{
    public UpdateMealDetailsValidator()
    {
        this.ValidMealDetails(x => x.Name, x => x.Description, x => x.Icon, x => x.Color, x => x.EatingOut);
    }
}
