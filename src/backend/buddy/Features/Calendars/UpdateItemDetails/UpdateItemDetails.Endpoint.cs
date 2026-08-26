using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class UpdateItemDetailsEndpoint
{
    public static RouteGroupBuilder MapUpdateItemDetails(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/details", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            UpdateItemDetailsRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateItemDetails.FromClaims(
                principal,
                new CalendarId(calendarId),
                new CalendarItemId(itemId),
                request.Title,
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
                // UpdateItemDetailsHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<CalendarItem>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCalendarItemDetails");

        return calendars;
    }
}

public sealed record UpdateItemDetailsRequest(string Title, string? Icon, string Color);
