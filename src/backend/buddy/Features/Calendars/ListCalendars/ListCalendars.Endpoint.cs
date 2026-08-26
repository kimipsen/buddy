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
                // m.Icon is null for a document written before Icon existed on this record (see
                // CalendarMembershipDocument) -- that calendar's actual icon is still the default,
                // it just was never written into this cached row.
                [.. memberships.Select(m => new CalendarSummaryResponse(new CalendarId(m.CalendarId), m.CalendarName, m.Icon ?? Calendar.DefaultIcon.Value, m.Role))]);
        })
        .WithName("ListCalendars");

        return calendars;
    }
}

public sealed record CalendarSummaryResponse(CalendarId Id, string Name, string Icon, CalendarRole Role);
