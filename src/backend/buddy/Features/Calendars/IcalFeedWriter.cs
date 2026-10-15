using System.Diagnostics;

using buddy.Common.Ical;

using Ical.Net;
using Ical.Net.CalendarComponents;
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
                    var icsEvent = new IcsEvent
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
                    };
                    AddAlarms(icsEvent.Alarms, occurrence);
                    calendar.Events.Add(icsEvent);
                    break;

                // A plain task is due at its instant; a routine subtask is a to-do due at its start.
                case (CalendarItemKind.Task, _):
                    var icsTodo = new IcsTodo
                    {
                        Uid = BuildUid(occurrence.ItemId, occurrence.SortAt),
                        Summary = occurrence.Title,
                        Due = ToCalDateTime(occurrence.SortAt, occurrence.IsAllDay),
                        DtStamp = stamp,
                    };
                    AddAlarms(icsTodo.Alarms, occurrence);
                    calendar.Todos.Add(icsTodo);
                    break;

                default:
                    throw new UnreachableException($"An event occurrence must be timed: {occurrence.ItemId.Value}.");
            }
        }

        // SerializeToString is annotated as returning string?, but never actually returns null
        // for a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    // One VALARM per offset, relative to the component's DTSTART (an event) or DUE (a to-do, which
    // has no DTSTART), so it follows the occurrence it sits on. DISPLAY needs a DESCRIPTION
    // (RFC 5545 3.6.6); the title is what the subscriber's app shows. AUDIO and EMAIL are not used
    // (docs/backend/analysis/calendar-item-alarms.md, Question 3).
    private static void AddAlarms(ICollection<Alarm> alarms, CalendarItemOccurrence occurrence)
    {
        foreach (var offset in occurrence.Alarms)
        {
            alarms.Add(new Alarm
            {
                Action = AlarmAction.Display,
                Description = occurrence.Title,
                Trigger = new Trigger(ToDuration(offset)),
            });
        }
    }

    // Nominal days, then hours and minutes, every part with the offset's sign: -1440 is -P1D, +540
    // is PT9H, -2340 is -P1DT15H, 0 is P0D (Ical.Net's zero, valid RFC 5545). A nominal day keeps
    // the wall-clock time across a DST change.
    private static Duration ToDuration(int offsetMinutes)
    {
        var sign = Math.Sign(offsetMinutes);
        var minutes = Math.Abs(offsetMinutes);

        return new Duration(
            days: sign * (minutes / (24 * 60)),
            hours: sign * (minutes % (24 * 60) / 60),
            minutes: sign * (minutes % 60));
    }

    private static CalDateTime ToCalDateTime(DateTimeOffset instant, bool isAllDay) =>
        isAllDay ? new CalDateTime(DateOnly.FromDateTime(instant.DateTime)) : new CalDateTime(instant.UtcDateTime, "UTC");

    // Each occurrence of a recurring item needs its own UID (RFC 5545 identifies a single
    // VEVENT/VTODO by UID) -- item id plus resolved instant is stable across regenerations of the
    // feed since occurrences are never persisted, only recomputed.
    private static string BuildUid(CalendarItemId itemId, DateTimeOffset instant) =>
        $"{itemId.Value:N}-{instant.UtcTicks}@buddy";
}
