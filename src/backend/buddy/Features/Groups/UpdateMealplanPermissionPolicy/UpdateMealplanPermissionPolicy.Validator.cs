using buddy.Features.Mealplans;

using FluentValidation;

namespace buddy.Features.Groups;

public sealed class UpdateMealplanPermissionPolicyValidator : AbstractValidator<UpdateMealplanPermissionPolicy>
{
    public UpdateMealplanPermissionPolicyValidator()
    {
        RuleFor(x => x.Policy).DefinedRolesAndTiers<UpdateMealplanPermissionPolicy, MealplanAccessTier>();
    }
}
