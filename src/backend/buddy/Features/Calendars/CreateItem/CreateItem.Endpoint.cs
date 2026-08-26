using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class CreateItemEndpoint
{
    public static RouteGroupBuilder MapCreateItem(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/{calendarId:guid}/items", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            CreateItemRequest request,
            IMessageBus bus,
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
                request.DueDate,
                request.IsAllDay,
                request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null);

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var message) => TypedResults.BadRequest(message),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreateCalendarItem");

        return calendars;
    }
}

public sealed record RecurrenceRuleRequest(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until);

public sealed record CreateItemRequest(
    CalendarItemKind Kind,
    string Title,
    string? Icon,
    string Color,
    StartsAt? StartsAt,
    EndsAt? EndsAt,
    DueDate? DueDate,
    bool IsAllDay,
    RecurrenceRuleRequest? Recurrence);

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
    Guid LastModifiedBy)
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
        item.LastModifiedBy.Value);
}
