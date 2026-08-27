using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class ListMealPlanValidator : AbstractValidator<ListMealPlan>
{
    public ListMealPlanValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListMealPlanHandler.MaxRangeDays);
    }
}
