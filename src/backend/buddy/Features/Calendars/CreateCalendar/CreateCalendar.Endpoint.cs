using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class CreateCalendarEndpoint
{
    public static RouteGroupBuilder MapCreateCalendar(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/", async Task<Results<Ok<CalendarResponse>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            CreateCalendarRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateCalendar.FromClaims(principal, request.Name);
            var calendar = await bus.InvokeAsync<Calendar?>(command, cancellationToken);

            if (calendar is null)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Ok(CalendarResponse.FromCalendar(calendar));
        })
        .WithName("CreateCalendar");

        return calendars;
    }
}

public sealed record CreateCalendarRequest(string Name);
