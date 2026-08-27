using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class CreateMealForGroupEndpoint
{
    public static RouteGroupBuilder MapCreateMealForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/groups/{groupId:guid}/meals", async Task<Results<Ok<MealResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            CreateMealRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMealForGroup.FromClaims(
                principal,
                new GroupId(groupId),
                request.Name,
                request.Description,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<Meal>>(command, cancellationToken);

            return result switch
            {
                Result<Meal>.Success(var meal) => TypedResults.Ok(MealResponse.FromMeal(meal)),
                Result<Meal>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Meal>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<Meal>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("CreateMealForGroup");

        return mealplans;
    }
}
