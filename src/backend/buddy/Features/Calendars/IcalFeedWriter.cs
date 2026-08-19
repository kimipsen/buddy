using Ical.Net.DataTypes;
using Ical.Net.Serialization;

using IcsCalendar = Ical.Net.Calendar;
using IcsEvent = Ical.Net.CalendarComponents.CalendarEvent;
using IcsTodo = Ical.Net.CalendarComponents.Todo;

namespace buddy.Features.Calendars;

// Aliased above because Ical.Net.Calendar and Ical.Net.CalendarComponents.CalendarEvent collide
// by name with our own Calendar aggregate and CalendarEvent union.
public static class IcalFeedWriter
{
    public static string Write(string calendarName, IReadOnlyCollection<CalendarItemOccurrence> occurrences)
    {
        var calendar = new IcsCalendar();
        calendar.AddProperty("X-WR-CALNAME", calendarName);

        var stamp = new CalDateTime(DateTime.UtcNow, "UTC");

        foreach (var occurrence in occurrences)
        {
            if (occurrence.Kind == CalendarItemKind.Event)
            {
                calendar.Events.Add(new IcsEvent
                {
                    Uid = BuildUid(occurrence.ItemId, occurrence.StartsAt!.Value),
                    Summary = occurrence.Title,
                    DtStart = new CalDateTime(occurrence.StartsAt.Value.UtcDateTime, "UTC"),
                    DtEnd = new CalDateTime(occurrence.EndsAt!.Value.UtcDateTime, "UTC"),
                    DtStamp = stamp,
                });
            }
            else
            {
                calendar.Todos.Add(new IcsTodo
                {
                    Uid = BuildUid(occurrence.ItemId, occurrence.DueAt!.Value),
                    Summary = occurrence.Title,
                    Due = new CalDateTime(occurrence.DueAt!.Value.UtcDateTime, "UTC"),
                    DtStamp = stamp,
                });
            }
        }

        // SerializeToString is annotated as returning string?, but never actually returns null
        // for a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    // Each occurrence of a recurring item needs its own UID (RFC 5545 identifies a single
    // VEVENT/VTODO by UID) -- item id plus resolved instant is stable across regenerations of the
    // feed since occurrences are never persisted, only recomputed.
    private static string BuildUid(CalendarItemId itemId, DateTimeOffset instant) =>
        $"{itemId.Value:N}-{instant.UtcTicks}@buddy";
}
