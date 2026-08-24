using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class UpdateMealDetailsForGroupEndpoint
{
    public static RouteGroupBuilder MapUpdateMealDetailsForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPatch("/groups/{groupId:guid}/meals/{mealId:guid}/details", async Task<Results<Ok<MealResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid mealId,
            UpdateMealDetailsRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateMealDetailsForGroup.FromClaims(
                principal,
                new GroupId(groupId),
                new MealId(mealId),
                request.Name,
                request.Description,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<Meal>>(command, cancellationToken);

            return result switch
            {
                Result<Meal>.Success(var meal) => TypedResults.Ok(MealResponse.FromMeal(meal)),
                Result<Meal>.Validation(var message) => TypedResults.BadRequest(message),
                Result<Meal>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<Meal>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("UpdateMealDetailsForGroup");

        return mealplans;
    }
}
