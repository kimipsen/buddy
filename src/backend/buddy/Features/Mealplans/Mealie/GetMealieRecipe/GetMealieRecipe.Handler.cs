using buddy.Features.Guardians;

using Microsoft.Extensions.Caching.Memory;

namespace buddy.Features.Mealplans;

// Fetched live, cached for a few minutes per connection and recipe: Buddy stores only the link,
// never the recipe (docs/backend/analysis/mealie-integration.md, Question 4).
public static class GetMealieRecipeHandler
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public static async Task<MealieOutcome<MealieRecipeDetails>> Handle(
        GetMealieRecipe query,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<MealieRecipeDetails>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(query.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, MealieRecipeDetails>();
        }

        var fetched = await FetchAsync(connection, query.RecipeId, mealie, cache, cancellationToken);

        if (fetched is not MealieResponse<MealieRecipeDto>.Ok(var recipe))
        {
            return MealieOutcome.FromFailure<MealieRecipeDetails>(fetched.FailureOf());
        }

        var linked = await SearchMealieRecipesHandler.LinkedMealsByRecipeAsync(query.ChildId, meals, guardians, cancellationToken);
        var slug = recipe.Slug ?? "";

        return new MealieOutcome<MealieRecipeDetails>.Success(new MealieRecipeDetails(
            recipe.Id,
            slug,
            recipe.Name ?? "",
            recipe.Description ?? "",
            recipe.TotalTime ?? "",
            recipe.RecipeYield ?? "",
            !string.IsNullOrEmpty(recipe.Image),
            [.. (recipe.RecipeIngredient ?? []).Select(IngredientText).Where(text => text.Length > 0)],
            [.. (recipe.RecipeInstructions ?? []).Select(s => new MealieRecipeStep(s.Title ?? "", s.Text ?? "")).Where(s => s.Text.Length > 0)],
            recipe.OrgUrl ?? "",
            MealieConnectionResolution.RecipeWebUrl(connection.Details, slug),
            linked.GetValueOrDefault(recipe.Id)));
    }

    // Shared with LinkMealToRecipe and CreateMealFromRecipe, which read the recipe's name and slug.
    // Keyed by when the connection was made, so a reconnect (another server or token) never serves
    // a recipe cached from the previous one.
    internal static async Task<MealieResponse<MealieRecipeDto>> FetchAsync(
        ActiveMealieConnection connection, Guid recipeId, IMealieClient mealie, IMemoryCache cache, CancellationToken cancellationToken)
    {
        var key = ("mealie-recipe", connection.Id.Value, connection.Details.ConnectedAt, recipeId);

        if (cache.TryGetValue(key, out MealieRecipeDto? cached) && cached is not null)
        {
            return new MealieResponse<MealieRecipeDto>.Ok(cached);
        }

        var fetched = await mealie.GetRecipeAsync(connection.Endpoint, recipeId, cancellationToken);

        if (fetched is MealieResponse<MealieRecipeDto>.Ok(var recipe))
        {
            cache.Set(key, recipe, CacheFor);
        }

        return fetched;
    }

    // Mealie's own rendering ("500 g minced beef"); a section title stands alone ("For the sauce").
    private static string IngredientText(MealieIngredientDto ingredient) =>
        (ingredient.Display ?? ingredient.Note ?? "").Trim() is { Length: > 0 } text ? text : (ingredient.Title ?? "").Trim();
}
