using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class CreateNightIcalTokenEndpoint
{
    public static RouteGroupBuilder MapCreateNightIcalToken(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapPost("/children/{childId:guid}/ical-tokens", async Task<Results<Ok<NightIcalTokenResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateNightIcalToken.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IssuedNightIcalToken>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedNightIcalToken>.Success(var issued) => TypedResults.Ok(new NightIcalTokenResponse(
                    issued.TokenId.Value,
                    issued.Token,
                    $"/night-duty/feeds/{issued.FeedId.Value}/ical/{issued.Token}",
                    issued.IssuedAt)),
                Result<IssuedNightIcalToken>.NotFound => TypedResults.NotFound(),
                Result<IssuedNightIcalToken>.Forbidden => TypedResults.Forbid(),
                // The command has no input to validate -- collapsed to NotFound since this route
                // declares no 400.
                Result<IssuedNightIcalToken>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("CreateNightIcalToken");

        return nightDuty;
    }
}

// SubscriptionPath is the ready-to-paste feed URL path (relative -- prefix with this API's host).
public sealed record NightIcalTokenResponse(Guid TokenId, string Token, string SubscriptionPath, DateTimeOffset IssuedAt);
