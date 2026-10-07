using buddy.Common.Erasure;
using buddy.Features.Groups;

namespace buddy.Features.Calendars;

// The "calendars" section: the calendars the caller can see, with their role, and the items in them
// the caller created or last changed, plus every item assigned to one of their children (in any
// calendar). Not other people's items: a shared calendar is the family's plan, not the caller's
// data. No iCal token hashes.
public sealed class CalendarsPersonalDataExporter(
    ICalendarsStore store,
    ICalendarEventStore calendars,
    ICalendarItemEventStore items,
    IGroupEventStore groups) : IPersonalDataExporter
{
    public Type Store => typeof(ICalendarsStore);

    public string Section => "calendars";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        var memberships = await ListCalendarsHandler.Handle(new ListCalendars(subject.UserId), calendars, groups, cancellationToken);
        var exported = new Dictionary<CalendarItemId, CalendarItemResponse>();

        foreach (var membership in memberships)
        {
            foreach (var itemId in await items.ListIdsForCalendarAsync(new CalendarId(membership.CalendarId), cancellationToken))
            {
                if (await items.FindSnapshotAsync(itemId, cancellationToken) is { IsDeleted: false } item
                    && (item.CreatedBy == subject.UserId || item.LastModifiedBy == subject.UserId))
                {
                    exported[itemId] = CalendarItemResponse.FromItem(item);
                }
            }
        }

        foreach (var childId in subject.Children)
        {
            foreach (var id in await CalendarsPersonalDataEraser.ListItemsAssignedToAsync(store, childId, cancellationToken))
            {
                var itemId = new CalendarItemId(id);

                if (await items.FindSnapshotAsync(itemId, cancellationToken) is { IsDeleted: false } item)
                {
                    exported[itemId] = CalendarItemResponse.FromItem(item);
                }
            }
        }

        return new CalendarsExport(
            [.. memberships.Select(m => new CalendarSummaryResponse(new CalendarId(m.CalendarId), m.CalendarName, m.Icon, m.Role))],
            [.. exported.Values]);
    }
}

public sealed record CalendarsExport(IReadOnlyList<CalendarSummaryResponse> Calendars, IReadOnlyList<CalendarItemResponse> Items);
