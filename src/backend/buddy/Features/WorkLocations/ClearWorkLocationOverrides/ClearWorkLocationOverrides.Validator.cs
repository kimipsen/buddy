using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public sealed class ClearWorkLocationOverridesValidator : AbstractValidator<ClearWorkLocationOverrides>
{
    public ClearWorkLocationOverridesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, WorkLocationRules.MaxRangeDays);
    }
}
