using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class LinkMealToRecipeValidator : AbstractValidator<LinkMealToRecipe>
{
    public LinkMealToRecipeValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
    }
}
