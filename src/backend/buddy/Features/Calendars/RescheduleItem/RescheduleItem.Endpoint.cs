using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class RescheduleItemEndpoint
{
    public static RouteGroupBuilder MapRescheduleItem(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/schedule", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            RescheduleItemRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RescheduleItem.FromClaims(
                principal,
                new CalendarId(calendarId),
                new CalendarItemId(itemId),
                request.StartsAt,
                request.EndsAt,
                request.DueAt);

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
        .WithName("RescheduleCalendarItem");

        return calendars;
    }
}

public sealed record RescheduleItemRequest(DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? DueAt);
