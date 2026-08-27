using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceEndpoint
{
    public static RouteGroupBuilder MapUpdateItemRecurrence(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/recurrence", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            UpdateItemRecurrenceRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var recurrence = request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null;
            var command = UpdateItemRecurrence.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId), recurrence);
            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCalendarItemRecurrence");

        return calendars;
    }
}

public sealed record UpdateItemRecurrenceRequest(RecurrenceRuleRequest? Recurrence);
