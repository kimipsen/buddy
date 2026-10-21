using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetMealieRecipeImageEndpoint
{
    public static RouteGroupBuilder MapGetMealieRecipeImage(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/mealie/recipes/{recipeId:guid}/image", async Task<Results<FileContentHttpResult, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>, JsonHttpResult<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid recipeId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<MealieOutcome<MealieImage>>(GetMealieRecipeImage.FromClaims(principal, new UserId(childId), recipeId), cancellationToken);

            if (outcome is MealieOutcome<MealieImage>.Success)
            {
                // A recipe photo rarely changes; private, because it comes through a signed-in route.
                httpContext.Response.Headers.CacheControl = "private, max-age=3600";
            }

            return outcome switch
            {
                MealieOutcome<MealieImage>.Success(var image) => TypedResults.File(image.Content, image.ContentType),
                MealieOutcome<MealieImage>.Forbidden => TypedResults.Forbid(),
                MealieOutcome<MealieImage>.NotConnected => TypedResults.Conflict(MealieOutcomeHttp.NotConnectedEnvelope(httpContext)),
                MealieOutcome<MealieImage>.UpstreamFailed(var failure) =>
                    TypedResults.Json(MealieOutcomeHttp.UpstreamEnvelope(failure, httpContext), statusCode: StatusCodes.Status502BadGateway),
                // Validation is never produced: the handler has no validator.
                MealieOutcome<MealieImage>.NotFound or MealieOutcome<MealieImage>.Validation => TypedResults.NotFound(),
            };
        })
        .RequireRateLimiting(RateLimitingFeature.MealieImagesPolicy)
        .ProducesMealieErrors()
        .WithName("GetMealieRecipeImage");

        return mealplans;
    }
}
