using System.Globalization;

using FluentValidation;

namespace buddy.Features.Calendars;

// The structural rules on an alarm list, shared by CreateItemValidator,
// ScheduleTaskFromTemplateValidator and UpdateItemAlarmsValidator. The list is already normalized
// (ItemAlarms.From), so the count is of distinct offsets: a repeated offset is merged, not rejected.
public static class ItemAlarmRules
{
    public const string Key = "alarms";

    private static readonly string TooMany = string.Create(CultureInfo.InvariantCulture, $"An item can have at most {ItemAlarms.Max} alarms.");

    private static readonly string OutOfRange = string.Create(
        CultureInfo.InvariantCulture,
        $"Each alarm must be between {ItemAlarms.MinOffset} and {ItemAlarms.MaxOffset} minutes from the item's start.");

    public static void ValidAlarms<T>(this IRuleBuilder<T, ItemAlarms> rule) =>
        rule.Custom((alarms, context) =>
        {
            if (alarms.OffsetMinutes.Length > ItemAlarms.Max)
            {
                context.AddFailure(Key, TooMany);
            }

            if (alarms.OffsetMinutes.Any(offset => offset is < ItemAlarms.MinOffset or > ItemAlarms.MaxOffset))
            {
                context.AddFailure(Key, OutOfRange);
            }
        });
}
