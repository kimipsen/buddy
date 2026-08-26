using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class UpdateCalendarIconEndpoint
{
    public static RouteGroupBuilder MapUpdateCalendarIcon(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/icon", async Task<Results<Ok<CalendarResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            UpdateCalendarIconRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateCalendarIcon.FromClaims(principal, new CalendarId(calendarId), new Icon(request.Icon));
            var result = await bus.InvokeAsync<Result<Calendar>>(command, cancellationToken);

            return result switch
            {
                Result<Calendar>.Success(var calendar) => TypedResults.Ok(CalendarResponse.FromCalendar(calendar)),
                Result<Calendar>.Forbidden => TypedResults.Forbid(),
                Result<Calendar>.NotFound => TypedResults.NotFound(),
                Result<Calendar>.Validation(var message) => TypedResults.BadRequest(message),
            };
        })
        .WithName("UpdateCalendarIcon");

        return calendars;
    }
}

public sealed record UpdateCalendarIconRequest(string Icon);
