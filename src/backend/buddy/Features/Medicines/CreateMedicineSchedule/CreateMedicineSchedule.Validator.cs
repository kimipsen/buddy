using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class CreateMedicineScheduleValidator : AbstractValidator<CreateMedicineSchedule>
{
    public CreateMedicineScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).MaximumLength(200);

        // Both optional; the endpoint trims them.
        RuleFor(x => x.Icon.Value).ValidSizedIcon().OverridePropertyName("Icon");
        RuleFor(x => x.Color.Value).MaximumLength(Color.MaxLength).OverridePropertyName("Color");

        RuleFor(x => x.Times)
            .NotEmpty()
            .WithMessage("A medicine schedule requires at least one dose time.");

        RuleFor(x => x.EndDate)
            .Must((command, end) => end is null || end >= command.StartDate)
            .WithMessage("The end date cannot be before the start date.");
    }
}
