using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

using WeekdaySet = buddy.Features.Calendars.Weekdays;

namespace buddy.Features.Calendars;

public static class CreateItemEndpoint
{
    public static RouteGroupBuilder MapCreateItem(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/{calendarId:guid}/items", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            CreateItemRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateItem.FromClaims(
                principal,
                new CalendarId(calendarId),
                request.Title,
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                new Color(request.Color),
                request.Schedule.ToSchedule(),
                RecurrenceRuleRequest.ToRecurrence(request.Recurrence));

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreateCalendarItem");

        return calendars;
    }
}

// The wire form of Recurrence: null for a one-off item, and a null Until for one that never ends.
// Weekdays (0 = Sunday ... 6 = Saturday) is null for no filter: a daily rule every day, a weekly
// one on the seed's weekday. A daily rule listing all seven days normalises to null, so "every
// day" has one representation; a weekly one keeps them (every N weeks, all week).
public sealed record RecurrenceRuleRequest(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until = null, IReadOnlyList<DayOfWeek>? Weekdays = null)
{
    // A bit outside Weekdays.All, which RecurrenceRules rejects as a 400: an empty list or a day
    // number outside 0-6 must not read as "no filter".
    private const WeekdaySet Invalid = (WeekdaySet)(1 << 7);

    public static Recurrence ToRecurrence(RecurrenceRuleRequest? request) => request is null
        ? new Recurrence.OneOff()
        : new Recurrence.Repeating(
            request.Frequency,
            request.IntervalCount,
            request.Until is { } until ? new RecurrenceEnd.On(until) : new RecurrenceEnd.Never(),
            ToWeekdays(request.Frequency, request.Weekdays));

    public static RecurrenceRuleRequest? From(Recurrence recurrence) => recurrence switch
    {
        Recurrence.OneOff => null,
        Recurrence.Repeating repeating => new RecurrenceRuleRequest(
            repeating.Frequency,
            repeating.IntervalCount,
            repeating.End switch
            {
                RecurrenceEnd.Never => null,
                RecurrenceEnd.On on => on.Until,
            },
            repeating.Weekdays == WeekdaySet.None ? null : repeating.Weekdays.ToDays()),
    };

    private static WeekdaySet ToWeekdays(RecurrenceFrequency frequency, IReadOnlyList<DayOfWeek>? days)
    {
        if (days is null)
        {
            return WeekdaySet.None;
        }

        var weekdays = days.Count == 0
            ? Invalid
            : days.Aggregate(WeekdaySet.None, (set, day) => set | (Enum.IsDefined(day) ? day.ToWeekday() : Invalid));

        return frequency == RecurrenceFrequency.Daily && weekdays == WeekdaySet.All ? WeekdaySet.None : weekdays;
    }
}

public sealed record CreateItemRequest(
    string Title,
    string Color,
    ItemScheduleRequest Schedule,
    string? Icon = null,
    RecurrenceRuleRequest? Recurrence = null);

// Icon is null when the item has no override -- it inherits the owning calendar's icon. This
// mirrors CalendarItem.Icon exactly (no calendar lookup happens here); the resolved/effective
// icon is only exposed on CalendarItemOccurrence, the rendering-ready projection.
public sealed record CalendarItemResponse(
    CalendarItemId Id,
    CalendarId CalendarId,
    string Title,
    string? Icon,
    string Color,
    ItemScheduleResponse Schedule,
    RecurrenceRuleRequest? Recurrence,
    Guid CreatedBy,
    Guid LastModifiedBy)
{
    public static CalendarItemResponse FromItem(CalendarItem item) => new(
        item.Id,
        item.CalendarId,
        item.Title,
        item.Icon?.Value,
        item.Color.Value,
        ItemScheduleResponse.From(item.Schedule),
        RecurrenceRuleRequest.From(item.Recurrence),
        item.CreatedBy.Value,
        item.LastModifiedBy.Value);
}
