using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class ListMealPlanForGroupValidator : AbstractValidator<ListMealPlanForGroup>
{
    public ListMealPlanForGroupValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListMealPlanHandler.MaxRangeDays);
    }
}
