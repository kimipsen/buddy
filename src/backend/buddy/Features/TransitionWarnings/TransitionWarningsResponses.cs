using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// GET /transition-warnings/me: what the child's device needs to compute warnings. Overrides carry
// no titles; the device already has the occurrences. LeadMinutes are largest first; an override's
// empty LeadMinutes means "never warn for this item".
public sealed record MyTransitionWarningsResponse(
    bool Enabled,
    IReadOnlyList<int> LeadMinutes,
    IReadOnlyList<WarningKind> Kinds,
    QuietHours? QuietHours,
    IReadOnlyList<ItemWarningOverrideResponse> Overrides)
{
    public static MyTransitionWarningsResponse From(TransitionWarningSettings settings) => new(
        settings.Defaults.Enabled,
        settings.Defaults.LeadMinutes,
        settings.Defaults.Kinds,
        settings.Defaults.QuietHours,
        [.. settings.ItemOverrides
            .OrderBy(entry => entry.Key.Value)
            .Select(entry => new ItemWarningOverrideResponse(entry.Key.Value, entry.Value.ToLeadMinutes()))]);
}

public sealed record ItemWarningOverrideResponse(Guid ItemId, IReadOnlyList<int> LeadMinutes);

// The guardian's view: the same settings, with each override's item title, effective icon and kind
// for the exceptions list. Overrides for deleted items, or items the guardian can no longer see,
// are left out (Question 4: orphans are harmless).
public sealed record ChildTransitionWarningsResponse(
    Guid ChildId,
    bool Enabled,
    IReadOnlyList<int> LeadMinutes,
    IReadOnlyList<WarningKind> Kinds,
    QuietHours? QuietHours,
    IReadOnlyList<ItemWarningOverrideDetailResponse> Overrides);

public sealed record ItemWarningOverrideDetailResponse(Guid ItemId, string Title, string Icon, CalendarItemKind Kind, IReadOnlyList<int> LeadMinutes);

public static class ChildTransitionWarningsView
{
    public static async Task<ChildTransitionWarningsResponse> BuildAsync(
        TransitionWarningSettings settings,
        UserId guardianId,
        ICalendarItemEventStore items,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        List<ItemWarningOverrideDetailResponse> overrides = [];
        // Exceptions cluster in a few calendars: each calendar and its access check are read once.
        Dictionary<CalendarId, Calendar?> visibleCalendars = [];

        foreach (var (itemId, itemOverride) in settings.ItemOverrides)
        {
            if (await items.FindSnapshotAsync(itemId, cancellationToken) is not { IsDeleted: false } item)
            {
                continue;
            }

            if (!visibleCalendars.TryGetValue(item.CalendarId, out var calendar))
            {
                calendar = await FindVisibleCalendarAsync(item.CalendarId, guardianId, calendars, groups, cancellationToken);
                visibleCalendars[item.CalendarId] = calendar;
            }

            if (calendar is not null)
            {
                overrides.Add(new ItemWarningOverrideDetailResponse(
                    itemId.Value,
                    item.Title,
                    item.Icon?.Value ?? calendar.Icon.Value,
                    item.Kind,
                    itemOverride.ToLeadMinutes()));
            }
        }

        var defaults = settings.Defaults;

        return new ChildTransitionWarningsResponse(
            settings.ChildId.Value,
            defaults.Enabled,
            defaults.LeadMinutes,
            defaults.Kinds,
            defaults.QuietHours,
            [.. overrides.OrderBy(o => o.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(o => o.ItemId)]);
    }

    // The item and its calendar, when the item exists, isn't deleted and sits in a calendar the
    // guardian can view; null otherwise -- missing and private collapse together, as everywhere.
    public static async Task<(CalendarItem Item, Calendar Calendar)?> FindVisibleItemAsync(
        CalendarItemId itemId,
        UserId guardianId,
        ICalendarItemEventStore items,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await items.FindSnapshotAsync(itemId, cancellationToken) is not { IsDeleted: false } item
            || await FindVisibleCalendarAsync(item.CalendarId, guardianId, calendars, groups, cancellationToken) is not { } calendar)
        {
            return null;
        }

        return (item, calendar);
    }

    private static async Task<Calendar?> FindVisibleCalendarAsync(
        CalendarId calendarId,
        UserId guardianId,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken) =>
        await calendars.FindSnapshotAsync(calendarId, cancellationToken) is { IsDeleted: false } calendar
            && await CalendarAuthorization.CheckView(calendar, guardianId, groups, cancellationToken) == CalendarAccess.Allowed
            ? calendar
            : null;
}
