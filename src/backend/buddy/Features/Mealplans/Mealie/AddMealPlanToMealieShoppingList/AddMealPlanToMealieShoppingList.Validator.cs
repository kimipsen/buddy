using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class AddMealPlanToMealieShoppingListValidator : AbstractValidator<AddMealPlanToMealieShoppingList>
{
    // A month: shopping is planned a week or two ahead.
    public const int MaxDays = 31;

    public AddMealPlanToMealieShoppingListValidator()
    {
        RuleFor(x => x.ListId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.To)
            .Must((command, to) => to.DayNumber - command.From.DayNumber < MaxDays)
            .WithMessage($"A shopping list covers at most {MaxDays} days.");
        RuleFor(x => x.Slots).NotEmpty();
        RuleForEach(x => x.Slots).IsInEnum();
    }
}
