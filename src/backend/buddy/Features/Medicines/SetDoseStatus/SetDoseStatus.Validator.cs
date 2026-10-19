using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class SetDoseStatusValidator : AbstractValidator<SetDoseStatus>
{
    public SetDoseStatusValidator()
    {
        // JsonStringEnumConverter also accepts integers, so an undefined one would bind.
        RuleFor(x => x.Status).IsInEnum();
    }
}
