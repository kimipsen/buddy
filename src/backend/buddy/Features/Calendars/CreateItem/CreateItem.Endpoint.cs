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
                request.Kind,
                request.Title,
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                new Color(request.Color),
                request.StartsAt,
                request.EndsAt,
                request.DueDate?.ToDueDate(request.IsAllDay),
                request.IsAllDay,
                request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null,
                request.AssignedTo is { } assignedTo ? new UserId(assignedTo) : null);

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

// The wire shape of a task's due date: all-day-ness travels as the request's own IsAllDay flag,
// not inside this object, so the endpoint builds the domain DueDate from both.
public sealed record DueDateRequest(DateOnly Date, TimeOnly Time)
{
    public DueDate ToDueDate(bool isAllDay) => new(Date, Time, isAllDay);
}

public sealed record RecurrenceRuleRequest(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until = null);

public sealed record CreateItemRequest(
    CalendarItemKind Kind,
    string Title,
    string Color,
    bool IsAllDay,
    string? Icon = null,
    StartsAt? StartsAt = null,
    EndsAt? EndsAt = null,
    DueDateRequest? DueDate = null,
    RecurrenceRuleRequest? Recurrence = null,
    // Only meaningful for a Task -- ignored for an Event. Null means unassigned.
    Guid? AssignedTo = null);

// Icon is null when the item has no override -- it inherits the owning calendar's icon. This
// mirrors CalendarItem.Icon exactly (no calendar lookup happens here); the resolved/effective
// icon is only exposed on CalendarItemOccurrence, the rendering-ready projection.
public sealed record CalendarItemResponse(
    CalendarItemId Id,
    CalendarId CalendarId,
    CalendarItemKind Kind,
    string Title,
    string? Icon,
    string Color,
    Period? Period,
    DueDate? DueDate,
    RecurrenceRuleRequest? Recurrence,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo,
    Guid? TaskTemplateId)
{
    public static CalendarItemResponse FromItem(CalendarItem item) => new(
        item.Id,
        item.CalendarId,
        item.Kind,
        item.Title,
        item.Icon?.Value,
        item.Color.Value,
        item.Period,
        item.DueDate,
        item.Recurrence is { } r ? new RecurrenceRuleRequest(r.Frequency, r.IntervalCount, r.Until) : null,
        item.CreatedBy.Value,
        item.LastModifiedBy.Value,
        item.AssignedTo?.Value,
        item.TaskTemplateId);
}
