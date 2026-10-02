using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.WorkLocations;

public sealed class ListWorkDaysValidator : AbstractValidator<ListWorkDays>
{
    public ListWorkDaysValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, WorkLocationRules.MaxRangeDays);
    }
}
