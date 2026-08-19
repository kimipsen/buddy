using System.Security.Claims;

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
                new Icon(request.Icon),
                new Color(request.Color),
                request.StartsAt,
                request.EndsAt,
                request.DueAt,
                request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null);

            var result = await bus.InvokeAsync<CreateItemResult>(command, cancellationToken);

            if (result.ValidationError is not null)
            {
                return TypedResults.BadRequest(result.ValidationError);
            }

            return result.Access switch
            {
                CalendarAccess.Allowed => TypedResults.Ok(CalendarItemResponse.FromItem(result.Item!)),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("CreateCalendarItem");

        return calendars;
    }
}

public sealed record RecurrenceRuleRequest(RecurrenceFrequency Frequency, int IntervalCount, DateTimeOffset? Until);

public sealed record CreateItemRequest(
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    RecurrenceRuleRequest? Recurrence);

public sealed record CalendarItemResponse(
    CalendarItemId Id,
    CalendarId CalendarId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    RecurrenceRuleRequest? Recurrence)
{
    public static CalendarItemResponse FromItem(CalendarItem item) => new(
        item.Id,
        item.CalendarId,
        item.Kind,
        item.Title,
        item.Icon.Value,
        item.Color.Value,
        item.StartsAt,
        item.EndsAt,
        item.DueAt,
        item.Recurrence is { } r ? new RecurrenceRuleRequest(r.Frequency, r.IntervalCount, r.Until) : null);
}
