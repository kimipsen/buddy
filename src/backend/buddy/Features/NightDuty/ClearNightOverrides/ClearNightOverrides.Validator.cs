using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.NightDuty;

public sealed class ClearNightOverridesValidator : AbstractValidator<ClearNightOverrides>
{
    public ClearNightOverridesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, NightDutyRules.MaxRangeDays);
    }
}
