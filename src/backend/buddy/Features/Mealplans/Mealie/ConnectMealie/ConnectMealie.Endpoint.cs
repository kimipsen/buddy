using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ConnectMealieEndpoint
{
    public static RouteGroupBuilder MapConnectMealie(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/mealie", async Task<Results<Ok<MealieConnectionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            ConnectMealieRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ConnectMealie.FromClaims(principal, new UserId(childId), FreeText.Normalize(request.BaseUrl), FreeText.Normalize(request.ApiToken));
            var result = await bus.InvokeAsync<Result<MealieConnectionView>>(command, cancellationToken);

            return result switch
            {
                Result<MealieConnectionView>.Success(var view) => TypedResults.Ok(view),
                Result<MealieConnectionView>.Forbidden => TypedResults.Forbid(),
                Result<MealieConnectionView>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MealieConnectionView>.NotFound => TypedResults.NotFound(),
            };
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .WithName("ConnectMealie");

        return mealplans;
    }
}

public sealed record ConnectMealieRequest(string BaseUrl, string ApiToken);
