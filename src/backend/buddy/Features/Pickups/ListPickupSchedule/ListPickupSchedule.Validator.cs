using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Pickups;

public sealed class ListPickupScheduleValidator : AbstractValidator<ListPickupSchedule>
{
    public ListPickupScheduleValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListPickupScheduleHandler.MaxRangeDays);
    }
}
