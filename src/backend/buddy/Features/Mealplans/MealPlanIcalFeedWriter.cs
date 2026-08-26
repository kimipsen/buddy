using Ical.Net.DataTypes;
using Ical.Net.Serialization;

using IcsCalendar = Ical.Net.Calendar;
using IcsEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace buddy.Features.Mealplans;

// Modeled on Features/Calendars/IcalFeedWriter.cs. Unlike that writer, times here are written as
// floating local time (no Z suffix, no TZID) rather than anchored to UTC -- MealPlan has no
// per-family timezone concept, and a meal time is inherently a local wall-clock fact (see
// docs/backend/analysis/mealplan-ical-feed.md).
public static class MealPlanIcalFeedWriter
{
    private static readonly TimeSpan EventDuration = TimeSpan.FromMinutes(30);

    public static string Write(MealPlan plan, IReadOnlyCollection<MealPlanEntry> entries)
    {
        var calendar = new IcsCalendar();
        calendar.AddProperty("X-WR-CALNAME", "Meal Plan");

        var stamp = new CalDateTime(DateTime.UtcNow, "UTC");

        foreach (var entry in entries)
        {
            var time = plan.SlotTimes.TryGetValue(entry.Slot, out var configured)
                ? configured
                : MealSlotDefaultTimes.Values[entry.Slot];

            var start = entry.Date.ToDateTime(time);

            calendar.Events.Add(new IcsEvent
            {
                Uid = BuildUid(entry),
                Summary = $"{entry.Slot}: {entry.MealName}",
                Description = entry.Notes,
                DtStart = new CalDateTime(start),
                DtEnd = new CalDateTime(start.Add(EventDuration)),
                DtStamp = stamp,
            });
        }

        // SerializeToString is annotated as returning string?, but never actually returns null for
        // a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    // Deterministic per (meal, date, slot) -- stable across feed regenerations since entries are
    // never persisted, only recomputed, mirroring IcalFeedWriter.BuildUid's rationale.
    private static string BuildUid(MealPlanEntry entry) =>
        $"{entry.MealId.Value:N}-{entry.Date:yyyyMMdd}-{entry.Slot}@buddy";
}
