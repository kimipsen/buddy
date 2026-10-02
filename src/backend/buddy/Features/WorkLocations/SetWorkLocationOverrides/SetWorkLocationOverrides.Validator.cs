using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public sealed class SetWorkLocationOverridesValidator : AbstractValidator<SetWorkLocationOverrides>
{
    public SetWorkLocationOverridesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, WorkLocationRules.MaxRangeDays);
    }
}
