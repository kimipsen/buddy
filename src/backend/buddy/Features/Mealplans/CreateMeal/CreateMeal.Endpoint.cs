using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class CreateMealEndpoint
{
    public static RouteGroupBuilder MapCreateMeal(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/meals", async Task<Results<Ok<MealResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CreateMealRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMeal.FromClaims(
                principal,
                new UserId(childId),
                request.Name,
                request.Description,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<Meal>>(command, cancellationToken);

            return result switch
            {
                Result<Meal>.Success(var meal) => TypedResults.Ok(MealResponse.FromMeal(meal)),
                Result<Meal>.Forbidden => TypedResults.Forbid(),
                Result<Meal>.Validation(var message) => TypedResults.BadRequest(message),
                Result<Meal>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreateMeal");

        return mealplans;
    }
}

public sealed record CreateMealRequest(string Name, string? Description, string Icon, string Color);

public sealed record MealRatingResponse(Guid ChildId, int Stars, string? Comment, DateTimeOffset RatedAt);

// No ChildId -- a Meal is shared by every child in its family (see MealFamilyResolution), so
// there's no single owning child to report. Ratings is every sibling's own rating, if any, useful
// to a guardian planning ahead ("Alice loved it, Bob didn't").
public sealed record MealResponse(
    MealId Id,
    string Name,
    string? Description,
    string Icon,
    string Color,
    bool IsArchived,
    IReadOnlyList<MealRatingResponse> Ratings,
    Guid CreatedBy,
    Guid LastModifiedBy)
{
    public static MealResponse FromMeal(Meal meal) => new(
        meal.Id,
        meal.Name,
        meal.Description,
        meal.Icon.Value,
        meal.Color.Value,
        meal.IsArchived,
        [.. meal.Ratings.Select(pair => new MealRatingResponse(pair.Key.Value, pair.Value.Stars, pair.Value.Comment, pair.Value.RatedAt))],
        meal.CreatedBy.Value,
        meal.LastModifiedBy.Value);
}
