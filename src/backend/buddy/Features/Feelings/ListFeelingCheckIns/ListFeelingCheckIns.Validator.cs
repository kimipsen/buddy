using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class ListFeelingCheckInsValidator : AbstractValidator<ListFeelingCheckIns>
{
    public ListFeelingCheckInsValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, FeelingsRules.MaxRangeDays);
    }
}
