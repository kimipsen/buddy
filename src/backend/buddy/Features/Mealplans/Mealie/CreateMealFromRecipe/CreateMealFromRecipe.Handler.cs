using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

using Microsoft.Extensions.Caching.Memory;

namespace buddy.Features.Mealplans;

// "Add from Mealie": a new library meal named and described after the recipe, linked to it, in one
// transaction ([MealCreated, MealLinkedToRecipe]). The icon and colour are the guardian's.
public static class CreateMealFromRecipeHandler
{
    public static async Task<MealieOutcome<Meal>> Handle(
        CreateMealFromRecipe command,
        IValidator<CreateMeal> mealValidator,
        IMealEventStore meals,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        IMemoryCache cache,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (command.RecipeId == Guid.Empty)
        {
            return new MealieOutcome<Meal>.Validation(new ValidationProblem(new Dictionary<string, string[]> { ["recipeId"] = ["'Recipe Id' must not be empty."] }));
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<Meal>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(command.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, Meal>();
        }

        var fetched = await GetMealieRecipeHandler.FetchAsync(connection, command.RecipeId, mealie, cache, cancellationToken);

        if (fetched is not MealieResponse<MealieRecipeDto>.Ok(var recipe))
        {
            return MealieOutcome.FromFailure<Meal>(fetched.FailureOf());
        }

        var name = Truncate((recipe.Name ?? "").Trim(), MealRules.MaxNameLength);
        var description = Truncate((recipe.Description ?? "").Trim(), MealRules.MaxDescriptionLength);

        // The meal rules every other create path uses; the name and description come from Mealie
        // and are cut to fit, so only the guardian's icon and colour can fail here.
        var asCreateMeal = new CreateMeal(command.UserId, command.ChildId, name, description, command.Icon, command.Color, MealEatingOut.None);

        if (await mealValidator.ValidateCommandAsync(asCreateMeal, cancellationToken) is { } problem)
        {
            return new MealieOutcome<Meal>.Validation(problem);
        }

        var mealId = MealId.New();
        var now = time.GetUtcNow();
        MealEvent[] events =
        [
            new MealCreated(mealId, command.ChildId, command.UserId, name, description, command.Icon, command.Color, now),
            new MealLinkedToRecipe(mealId, new MealieRecipeRef(recipe.Id, recipe.Slug ?? "", recipe.Name ?? ""), command.UserId, now)
        ];

        return new MealieOutcome<Meal>.Success(Meal.Replay(await meals.CreateAsync(mealId, events, cancellationToken)));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd();
}
