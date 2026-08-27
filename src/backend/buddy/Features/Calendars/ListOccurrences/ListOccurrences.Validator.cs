using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class ListOccurrencesValidator : AbstractValidator<ListOccurrences>
{
    public ListOccurrencesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListOccurrencesHandler.MaxRangeDays);
    }
}
