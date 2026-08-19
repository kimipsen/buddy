using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListCalendarsEndpoint
{
    public static RouteGroupBuilder MapListCalendars(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/", async Task<Ok<IReadOnlyCollection<CalendarSummaryResponse>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var memberships = await bus.InvokeAsync<IReadOnlyCollection<CalendarMembershipDocument>>(ListCalendars.FromClaims(principal), cancellationToken);

            return TypedResults.Ok<IReadOnlyCollection<CalendarSummaryResponse>>(
                [.. memberships.Select(m => new CalendarSummaryResponse(new CalendarId(m.CalendarId), m.CalendarName, m.Role))]);
        })
        .WithName("ListCalendars");

        return calendars;
    }
}

public sealed record CalendarSummaryResponse(CalendarId Id, string Name, CalendarRole Role);
