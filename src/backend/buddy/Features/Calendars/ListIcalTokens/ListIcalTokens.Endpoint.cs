using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListIcalTokensEndpoint
{
    public static RouteGroupBuilder MapListIcalTokens(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/ical-tokens", async Task<Results<Ok<IReadOnlyCollection<IcalTokenSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListIcalTokens.FromClaims(principal, new CalendarId(calendarId));
            var result = await bus.InvokeAsync<ListIcalTokensResult>(query, cancellationToken);

            return result.Access switch
            {
                CalendarAccess.Allowed => TypedResults.Ok(result.Tokens),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("ListCalendarIcalTokens");

        return calendars;
    }
}
