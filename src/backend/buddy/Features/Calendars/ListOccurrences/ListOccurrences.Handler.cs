using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class ListOccurrencesHandler
{
    // Keeps a single request's expansion work bounded regardless of how many recurring items a
    // calendar has.
    public const int MaxRangeDays = 366;

    public static async Task<Result<IReadOnlyCollection<CalendarItemOccurrence>>> Handle(
        ListOccurrences query,
        IValidator<ListOccurrences> validator,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.Validation(problem);
        }

        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<CalendarItemOccurrence>>();
        }

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(query.CalendarId, calendar!.TimeZoneId, calendar.Icon, query.From, query.To, items, cancellationToken);

        if (await ChildVisibility.IsChildAsync(userId, guardians, cancellationToken))
        {
            occurrences = ChildVisibility.FilterForChild(occurrences, userId);
        }

        return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.Success(occurrences);
    }
}
