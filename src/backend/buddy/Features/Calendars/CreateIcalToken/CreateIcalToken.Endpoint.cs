using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class CreateIcalTokenEndpoint
{
    public static RouteGroupBuilder MapCreateIcalToken(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/{calendarId:guid}/ical-tokens", async Task<Results<Ok<IcalTokenResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateIcalToken.FromClaims(principal, new CalendarId(calendarId));
            var result = await bus.InvokeAsync<Result<IssuedIcalToken>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedIcalToken>.Success(var issued) => TypedResults.Ok(new IcalTokenResponse(
                    issued.TokenId.Value,
                    issued.Token,
                    $"/calendars/{calendarId}/ical/{issued.Token}")),
                Result<IssuedIcalToken>.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("CreateCalendarIcalToken");

        return calendars;
    }
}

// Token is the plaintext subscription secret -- returned exactly once, here, and never again.
// The caller is responsible for storing it; SubscriptionPath is the ready-to-paste feed URL path
// (relative -- prefix with this API's host).
public sealed record IcalTokenResponse(Guid TokenId, string Token, string SubscriptionPath);
