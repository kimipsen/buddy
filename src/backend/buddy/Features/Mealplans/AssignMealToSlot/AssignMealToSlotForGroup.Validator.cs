using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class AssignMealToSlotForGroupValidator : AbstractValidator<AssignMealToSlotForGroup>
{
    public AssignMealToSlotForGroupValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
