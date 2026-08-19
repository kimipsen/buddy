using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceEndpoint
{
    public static RouteGroupBuilder MapUpdateItemRecurrence(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/recurrence", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            UpdateItemRecurrenceRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var recurrence = request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null;
            var command = UpdateItemRecurrence.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId), recurrence);
            var result = await bus.InvokeAsync<UpdateItemResult>(command, cancellationToken);

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
        .WithName("UpdateCalendarItemRecurrence");

        return calendars;
    }
}

public sealed record UpdateItemRecurrenceRequest(RecurrenceRuleRequest? Recurrence);
