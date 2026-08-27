using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class UpdateMedicineDetailsForGroupValidator : AbstractValidator<UpdateMedicineDetailsForGroup>
{
    public UpdateMedicineDetailsForGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).MaximumLength(200);
    }
}
