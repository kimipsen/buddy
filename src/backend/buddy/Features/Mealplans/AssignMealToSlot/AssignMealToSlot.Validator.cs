using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class AssignMealToSlotValidator : AbstractValidator<AssignMealToSlot>
{
    public AssignMealToSlotValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
