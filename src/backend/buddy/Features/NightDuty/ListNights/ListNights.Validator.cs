using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.NightDuty;

public sealed class ListNightsValidator : AbstractValidator<ListNights>
{
    public ListNightsValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, NightDutyRules.MaxRangeDays);
    }
}
