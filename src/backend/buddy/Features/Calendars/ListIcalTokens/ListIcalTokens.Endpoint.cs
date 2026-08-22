using System.Security.Claims;

using buddy.Common;

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
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<IcalTokenSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<IcalTokenSummary>>.Success(var tokens) => TypedResults.Ok(tokens),
                Result<IReadOnlyCollection<IcalTokenSummary>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<IcalTokenSummary>>.NotFound => TypedResults.NotFound(),
                // ListIcalTokensHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<IReadOnlyCollection<IcalTokenSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListCalendarIcalTokens");

        return calendars;
    }
}
