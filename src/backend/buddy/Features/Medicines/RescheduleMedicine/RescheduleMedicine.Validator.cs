using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class RescheduleMedicineValidator : AbstractValidator<RescheduleMedicine>
{
    public RescheduleMedicineValidator()
    {
        RuleFor(x => x.Times)
            .NotEmpty()
            .WithMessage("A medicine schedule requires at least one dose time.");

        RuleFor(x => x.EndDate)
            .Must((command, end) => end is null || end >= command.StartDate)
            .WithMessage("The end date cannot be before the start date.");
    }
}
