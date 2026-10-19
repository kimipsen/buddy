using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class UpdateMedicineDetailsValidator : AbstractValidator<UpdateMedicineDetails>
{
    public UpdateMedicineDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).MaximumLength(200);

        // Both optional; the endpoint trims them.
        RuleFor(x => x.Icon.Value).ValidSizedIcon().OverridePropertyName("Icon");
        RuleFor(x => x.Color.Value).MaximumLength(Color.MaxLength).OverridePropertyName("Color");
    }
}
