using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class SearchMealieRecipesValidator : AbstractValidator<SearchMealieRecipes>
{
    public const int MaxSearchLength = 200;
    public const int PageSize = 20;

    public SearchMealieRecipesValidator()
    {
        RuleFor(x => x.Search).MaximumLength(MaxSearchLength);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
