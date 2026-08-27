using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class UpdateMedicineDetailsValidator : AbstractValidator<UpdateMedicineDetails>
{
    public UpdateMedicineDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).MaximumLength(200);
    }
}
