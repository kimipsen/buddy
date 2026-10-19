using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class SetDoseStatusForGroupValidator : AbstractValidator<SetDoseStatusForGroup>
{
    public SetDoseStatusForGroupValidator()
    {
        // JsonStringEnumConverter also accepts integers, so an undefined one would bind.
        RuleFor(x => x.Status).IsInEnum();
    }
}
