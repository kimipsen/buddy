using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public sealed class GetSharedSleepDiaryValidator : AbstractValidator<GetSharedSleepDiary>
{
    public GetSharedSleepDiaryValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListSleepDiaryEntriesHandler.MaxRangeDays);
    }
}
