using System.Diagnostics;

using buddy.Common.Ical;

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
        IcalSubscription.Describe(calendar, calendarName);

        // Start of the UTC day, not UtcNow: an unchanged feed must render byte-identically within a
        // day or its ETag never matches. The feed window rolls daily anyway (see
        // docs/backend/analysis/conditional-get-etags.md).
        var stamp = new CalDateTime(DateTime.UtcNow.Date, "UTC");

        foreach (var occurrence in occurrences)
        {
            switch (occurrence.Kind, occurrence.Timing)
            {
                case (CalendarItemKind.Event, OccurrenceTiming.Timed timed):
                    calendar.Events.Add(new IcsEvent
                    {
                        Uid = BuildUid(occurrence.ItemId, timed.StartsAt),
                        Summary = occurrence.Title,
                        // An all-day event is written with date-only (VALUE=DATE) start/end -- the
                        // occurrence's own local date, not UtcDateTime, so the day doesn't shift across
                        // a UTC boundary. CalendarEvent.IsAllDay is computed from DtStart/DtEnd having
                        // no time component, so nothing else needs to be set for it to read as all-day.
                        DtStart = ToCalDateTime(timed.StartsAt, occurrence.IsAllDay),
                        DtEnd = ToCalDateTime(timed.EndsAt, occurrence.IsAllDay),
                        DtStamp = stamp,
                    });
                    break;

                // A plain task is due at its instant; a routine subtask is a to-do due at its start.
                case (CalendarItemKind.Task, _):
                    calendar.Todos.Add(new IcsTodo
                    {
                        Uid = BuildUid(occurrence.ItemId, occurrence.SortAt),
                        Summary = occurrence.Title,
                        Due = ToCalDateTime(occurrence.SortAt, occurrence.IsAllDay),
                        DtStamp = stamp,
                    });
                    break;

                default:
                    throw new UnreachableException($"An event occurrence must be timed: {occurrence.ItemId.Value}.");
            }
        }

        // SerializeToString is annotated as returning string?, but never actually returns null
        // for a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    private static CalDateTime ToCalDateTime(DateTimeOffset instant, bool isAllDay) =>
        isAllDay ? new CalDateTime(DateOnly.FromDateTime(instant.DateTime)) : new CalDateTime(instant.UtcDateTime, "UTC");

    // Each occurrence of a recurring item needs its own UID (RFC 5545 identifies a single
    // VEVENT/VTODO by UID) -- item id plus resolved instant is stable across regenerations of the
    // feed since occurrences are never persisted, only recomputed.
    private static string BuildUid(CalendarItemId itemId, DateTimeOffset instant) =>
        $"{itemId.Value:N}-{instant.UtcTicks}@buddy";
}
