using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Mealplans may read Calendars' types/stores but never the reverse (see
// docs/backend/analysis/mealplans.md) -- this is the one place that dependency is exercised, for
// the AI assistant's get_calendar_conflicts tool.
public static class CalendarConflictLookup
{
    public static async Task<IReadOnlyCollection<CalendarItemOccurrence>> FindOccurrencesAsync(
        UserId callerId,
        DateOnly from,
        DateOnly to,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        ITaskTemplateEventStore templates,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        // Reuses the exact same "which calendars can this user see" resolution the Calendars
        // feature itself uses for its own list view (own + group-derived + linked children's) --
        // being in this list already means at least View access, so no separate
        // CalendarAuthorization check is needed per calendar.
        var memberships = await ListCalendarsHandler.Handle(new ListCalendars(callerId), calendars, groups, guardians, cancellationToken);
        var calendarIds = memberships.Select(m => m.CalendarId).Distinct();

        List<CalendarItemOccurrence> occurrences = [];

        foreach (var rawCalendarId in calendarIds)
        {
            var calendarId = new CalendarId(rawCalendarId);
            var calendar = Calendar.Rehydrate(await calendars.ReadAsync(calendarId, cancellationToken));

            if (calendar is not { IsDeleted: false })
            {
                continue;
            }

            occurrences.AddRange(await CalendarOccurrenceExpansion.ExpandAsync(
                calendarId, calendar.TimeZoneId, calendar.Icon, from, to, items, templates, cancellationToken));
        }

        occurrences.Sort((a, b) => (a.StartsAt ?? a.DueAt)!.Value.CompareTo((b.StartsAt ?? b.DueAt)!.Value));

        return occurrences;
    }
}
