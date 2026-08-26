using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class CreateMealPlanIcalTokenEndpoint
{
    public static RouteGroupBuilder MapCreateMealPlanIcalToken(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ical-tokens", async Task<Results<Ok<MealPlanIcalTokenResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMealPlanIcalToken.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IssuedMealPlanIcalToken>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedMealPlanIcalToken>.Success(var issued) => TypedResults.Ok(new MealPlanIcalTokenResponse(
                    issued.TokenId.Value,
                    issued.Token,
                    $"/mealplans/{issued.MealPlanId.Value}/ical/{issued.Token}")),
                Result<IssuedMealPlanIcalToken>.Forbidden => TypedResults.Forbid(),
                Result<IssuedMealPlanIcalToken>.NotFound => TypedResults.NotFound(),
                // CreateMealPlanIcalTokenHandler never produces Validation -- there's no BadRequest
                // in this route's declared results, so this collapses to NotFound like the others.
                Result<IssuedMealPlanIcalToken>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("CreateMealPlanIcalToken");

        return mealplans;
    }
}

// SubscriptionPath is the ready-to-paste feed URL path (relative -- prefix with this API's host).
public sealed record MealPlanIcalTokenResponse(Guid TokenId, string Token, string SubscriptionPath);
