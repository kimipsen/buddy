using System.Linq.Expressions;

using FluentValidation;

namespace buddy.Common.Validation;

// Shared by every list-in-a-date-range query (ListOccurrences, ListTodaysDoses, ListMealPlan,
// ListPickupSchedule, and their ForGroup siblings). This is generic technical validation, not a
// domain concept, so centralizing it doesn't cross the vertical-slice boundaries
// docs/backend/analysis/pickup-schedules.md deliberately protects for business rules.
public static class DateRangeRules
{
    public static void ValidDateRange<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, DateOnly>> fromSelector,
        Expression<Func<T, DateOnly>> toSelector,
        int maxRangeDays)
    {
        var from = fromSelector.Compile();

        validator.RuleFor(toSelector)
            .Must((command, to) => to >= from(command))
            .WithMessage("'to' must not be before 'from'.");

        validator.RuleFor(toSelector)
            .Must((command, to) => to.DayNumber - from(command).DayNumber <= maxRangeDays)
            .WithMessage($"The requested range cannot exceed {maxRangeDays} days.");
    }
}
