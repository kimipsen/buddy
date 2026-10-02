using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

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
public sealed record RecurrenceRuleRequest(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until = null)
{
    public static Recurrence ToRecurrence(RecurrenceRuleRequest? request) => request is null
        ? new Recurrence.OneOff()
        : new Recurrence.Repeating(
            request.Frequency,
            request.IntervalCount,
            request.Until is { } until ? new RecurrenceEnd.On(until) : new RecurrenceEnd.Never());

    public static RecurrenceRuleRequest? From(Recurrence recurrence) => recurrence switch
    {
        Recurrence.OneOff => null,
        Recurrence.Repeating repeating => new RecurrenceRuleRequest(repeating.Frequency, repeating.IntervalCount, repeating.End switch
        {
            RecurrenceEnd.Never => null,
            RecurrenceEnd.On on => on.Until,
        }),
    };
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
