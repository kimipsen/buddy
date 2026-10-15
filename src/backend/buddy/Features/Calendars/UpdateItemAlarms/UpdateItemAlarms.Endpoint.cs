using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class UpdateItemAlarmsEndpoint
{
    public static RouteGroupBuilder MapUpdateItemAlarms(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/alarms", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            UpdateItemAlarmsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateItemAlarms.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId), ItemAlarms.From(request.Alarms));
            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCalendarItemAlarms");

        return calendars;
    }
}

// Signed whole minutes from the item's start or due time (negative = before). An empty or missing
// list clears every alarm; a repeated offset is stored once.
public sealed record UpdateItemAlarmsRequest(IReadOnlyList<int>? Alarms = null);
