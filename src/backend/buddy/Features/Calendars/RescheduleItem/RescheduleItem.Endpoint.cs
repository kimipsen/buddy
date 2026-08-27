using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class RescheduleItemEndpoint
{
    public static RouteGroupBuilder MapRescheduleItem(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/schedule", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            RescheduleItemRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = RescheduleItem.FromClaims(
                principal,
                new CalendarId(calendarId),
                new CalendarItemId(itemId),
                request.StartsAt,
                request.EndsAt,
                request.DueDate,
                request.IsAllDay);

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("RescheduleCalendarItem");

        return calendars;
    }
}

public sealed record RescheduleItemRequest(StartsAt? StartsAt, EndsAt? EndsAt, DueDate? DueDate, bool IsAllDay);
