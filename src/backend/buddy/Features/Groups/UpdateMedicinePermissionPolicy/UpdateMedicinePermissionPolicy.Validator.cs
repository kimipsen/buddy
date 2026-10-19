using buddy.Features.Medicines;

using FluentValidation;

namespace buddy.Features.Groups;

public sealed class UpdateMedicinePermissionPolicyValidator : AbstractValidator<UpdateMedicinePermissionPolicy>
{
    public UpdateMedicinePermissionPolicyValidator()
    {
        RuleFor(x => x.Policy).DefinedRolesAndTiers<UpdateMedicinePermissionPolicy, MedicineAccessTier>();
    }
}
