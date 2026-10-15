using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetOccurrenceSkipEndpoint
{
    public static RouteGroupBuilder MapSetOccurrenceSkip(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/skip", async Task<Results<Ok<OccurrenceSkipResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            SetOccurrenceSkipRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetOccurrenceSkip.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId), request.Date, request.IsSkipped);

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(new OccurrenceSkipResponse(
                    item.Id, request.Date, item.SkippedDates.Contains(request.Date))),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetOccurrenceSkip");

        return calendars;
    }
}

public sealed record SetOccurrenceSkipRequest(DateOnly Date, bool IsSkipped);

public sealed record OccurrenceSkipResponse(CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsSkipped);
