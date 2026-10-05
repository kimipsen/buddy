using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public sealed class ListSleepDiaryEntriesValidator : AbstractValidator<ListSleepDiaryEntries>
{
    public ListSleepDiaryEntriesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListSleepDiaryEntriesHandler.MaxRangeDays);
    }
}
