using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class UpdateMealDetailsEndpoint
{
    public static RouteGroupBuilder MapUpdateMealDetails(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPatch("/children/{childId:guid}/meals/{mealId:guid}/details", async Task<Results<Ok<MealResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid mealId,
            UpdateMealDetailsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateMealDetails.FromClaims(
                principal,
                new UserId(childId),
                new MealId(mealId),
                request.Name,
                request.Description,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<Meal>>(command, cancellationToken);

            return result switch
            {
                Result<Meal>.Success(var meal) => TypedResults.Ok(MealResponse.FromMeal(meal)),
                Result<Meal>.Forbidden => TypedResults.Forbid(),
                Result<Meal>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Meal>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateMealDetails");

        return mealplans;
    }
}

public sealed record UpdateMealDetailsRequest(string Name, string? Description, string Icon, string Color);
