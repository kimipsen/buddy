using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class CreateMedicineScheduleForGroupValidator : AbstractValidator<CreateMedicineScheduleForGroup>
{
    public CreateMedicineScheduleForGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).MaximumLength(200);

        RuleFor(x => x.Times)
            .NotEmpty()
            .WithMessage("A medicine schedule requires at least one dose time.");

        RuleFor(x => x.EndDate)
            .Must((command, end) => end is null || end >= command.StartDate)
            .WithMessage("The end date cannot be before the start date.");
    }
}
