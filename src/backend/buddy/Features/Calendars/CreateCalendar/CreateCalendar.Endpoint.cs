using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class CreateCalendarEndpoint
{
    public static RouteGroupBuilder MapCreateCalendar(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/", async Task<Results<Ok<CalendarResponse>, UnauthorizedHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            CreateCalendarRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateCalendar.FromClaims(principal, request.Name, new TimeZoneId(request.TimeZoneId));
            var result = await bus.InvokeAsync<CreateCalendarResult>(command, cancellationToken);

            if (result.Unauthenticated)
            {
                return TypedResults.Unauthorized();
            }

            if (result.ValidationError is not null)
            {
                return TypedResults.BadRequest(result.ValidationError);
            }

            return TypedResults.Ok(CalendarResponse.FromCalendar(result.Calendar!));
        })
        .WithName("CreateCalendar");

        return calendars;
    }
}

public sealed record CreateCalendarRequest(string Name, string TimeZoneId);
