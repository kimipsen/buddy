using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class RateMealEndpoint
{
    public static RouteGroupBuilder MapRateMeal(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/meals/{mealId:guid}/rating", async Task<Results<Ok<MealResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid mealId,
            RateMealRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = RateMeal.FromClaims(principal, new UserId(childId), new MealId(mealId), request.Stars, request.Comment);
            var result = await bus.InvokeAsync<Result<Meal>>(command, cancellationToken);

            return result switch
            {
                Result<Meal>.Success(var meal) => TypedResults.Ok(MealResponse.FromMeal(meal)),
                Result<Meal>.Forbidden => TypedResults.Forbid(),
                Result<Meal>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Meal>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("RateMeal");

        return mealplans;
    }
}

public sealed record RateMealRequest(int Stars, string? Comment);
