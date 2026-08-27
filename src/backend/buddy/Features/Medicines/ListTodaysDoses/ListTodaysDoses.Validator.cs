using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class ListTodaysDosesValidator : AbstractValidator<ListTodaysDoses>
{
    public ListTodaysDosesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListTodaysDosesHandler.MaxRangeDays);
    }
}
