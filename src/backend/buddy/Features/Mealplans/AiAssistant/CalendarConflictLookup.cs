using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// One occurrence as the AI assistant may see it. Title is null when the occurrence's title must not
// leave Buddy, and the tool sends "busy" instead (GDPR Question 6 in
// docs/backend/analysis/gdpr-data-protection.md).
public sealed record CalendarConflict(DateTimeOffset At, bool IsAllDay, string? Title);

// Mealplans may read Calendars' types/stores but never the reverse (see
// docs/backend/analysis/mealplans.md) -- this is the one place that dependency is exercised, for
// the AI assistant's get_calendar_conflicts tool.
public static class CalendarConflictLookup
{
    public static async Task<IReadOnlyCollection<CalendarConflict>> FindConflictsAsync(
        UserId callerId,
        UserId sessionChildId,
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
        // feature itself uses for its own list view (explicit + group-derived memberships) --
        // being in this list already means at least View access, so no separate
        // CalendarAuthorization check is needed per calendar.
        var memberships = await ListCalendarsHandler.Handle(new ListCalendars(callerId), calendars, groups, cancellationToken);
        var calendarIds = memberships.Select(m => m.CalendarId).Distinct();
        var family = await ResolveFamilyMembersAsync(sessionChildId, guardians, cancellationToken);

        List<(CalendarItemOccurrence Occurrence, bool IsFamilyCalendar)> occurrences = [];

        foreach (var rawCalendarId in calendarIds)
        {
            var calendarId = new CalendarId(rawCalendarId);
            var calendar = Calendar.Rehydrate(await calendars.ReadAsync(calendarId, cancellationToken));

            if (calendar is not { IsDeleted: false })
            {
                continue;
            }

            var isFamilyCalendar = await IsFamilyCalendarAsync(calendar, family, groups, cancellationToken);
            var expanded = await CalendarOccurrenceExpansion.ExpandAsync(
                calendarId, calendar.TimeZoneId, calendar.Icon, from, to, items, templates, cancellationToken);

            occurrences.AddRange(expanded.Select(o => (o, isFamilyCalendar)));
        }

        return
        [
            .. occurrences
                .OrderBy(o => o.Occurrence.SortAt)
                .Select(o => new CalendarConflict(
                    o.Occurrence.SortAt,
                    o.Occurrence.IsAllDay,
                    IsTitleShareable(o.Occurrence, o.IsFamilyCalendar, sessionChildId) ? o.Occurrence.Title : null))
        ];
    }

    // A title is sent only for an item in one of the child's family calendars that is assigned to
    // the session's child or to nobody. An item assigned to a sibling or a guardian, or anything in
    // a calendar shared with people outside the family, is sent as "busy".
    private static bool IsTitleShareable(CalendarItemOccurrence occurrence, bool isFamilyCalendar, UserId sessionChildId) =>
        isFamilyCalendar && (occurrence.AssignedTo is null || occurrence.AssignedTo == sessionChildId.Value);

    // The family is every child of the session child's active guardians (MealFamilyResolution)
    // plus those guardians.
    private static async Task<HashSet<UserId>> ResolveFamilyMembersAsync(
        UserId sessionChildId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var children = await MealFamilyResolution.ResolveFamilyAsync(sessionChildId, guardians, cancellationToken);
        var family = new HashSet<UserId>(children);

        foreach (var child in children)
        {
            foreach (var link in await guardians.ListForChildAsync(child, cancellationToken))
            {
                family.Add(new UserId(link.GuardianId));
            }
        }

        return family;
    }

    // A family calendar is one whose owning group and explicit members are all family. A calendar
    // shared with a school class, a club or another family is not, even if the child is in it.
    private static async Task<bool> IsFamilyCalendarAsync(
        Calendar calendar, HashSet<UserId> family, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (!calendar.Members.Keys.All(family.Contains))
        {
            return false;
        }

        var group = Group.Rehydrate(await groups.ReadAsync(calendar.GroupId, cancellationToken));

        return group is { IsDeleted: false } && group.Members.Keys.All(family.Contains);
    }
}
