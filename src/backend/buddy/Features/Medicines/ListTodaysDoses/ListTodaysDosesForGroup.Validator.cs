using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class ListTodaysDosesForGroupValidator : AbstractValidator<ListTodaysDosesForGroup>
{
    public ListTodaysDosesForGroupValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListTodaysDosesHandler.MaxRangeDays);
    }
}
