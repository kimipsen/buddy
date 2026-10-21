using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class SearchMealieRecipesHandler
{
    public static async Task<MealieOutcome<MealieRecipePage>> Handle(
        SearchMealieRecipes query,
        IValidator<SearchMealieRecipes> validator,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new MealieOutcome<MealieRecipePage>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<MealieRecipePage>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(query.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, MealieRecipePage>();
        }

        var response = await mealie.SearchRecipesAsync(connection.Endpoint, query.Search, query.Page, SearchMealieRecipesValidator.PageSize, cancellationToken);

        if (response is not MealieResponse<MealiePageDto<MealieRecipeSummaryDto>>.Ok(var page))
        {
            return MealieOutcome.FromFailure<MealieRecipePage>(response.FailureOf());
        }

        var linked = page.Items is { Count: > 0 }
            ? await LinkedMealsByRecipeAsync(query.ChildId, meals, guardians, cancellationToken)
            : [];

        MealieRecipeSummary[] items =
        [
            .. (page.Items ?? []).Select(r => new MealieRecipeSummary(
                r.Id,
                r.Slug ?? "",
                r.Name ?? "",
                r.Description ?? "",
                r.TotalTime ?? "",
                !string.IsNullOrEmpty(r.Image),
                linked.GetValueOrDefault(r.Id)))
        ];

        return new MealieOutcome<MealieRecipePage>.Success(new MealieRecipePage(items, page.Page, page.TotalPages, page.Total));
    }

    // Recipe id -> the family's active meal linked to it. An archived meal doesn't count: it's gone
    // from the library, so "Already linked" would point at a meal the guardian can't find, and
    // adding the recipe as a new meal is what they need.
    internal static async Task<Dictionary<Guid, MealId>> LinkedMealsByRecipeAsync(
        UserId childId, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        // Snapshots in one query rather than a replay per meal: this runs on every search.
        var mealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);
        var family = await meals.FindSnapshotsAsync(mealIds, cancellationToken);

        return family
            .Where(m => m.Recipe is not null && !m.IsArchived)
            .OrderBy(m => m.Id.Value)
            .GroupBy(m => m.Recipe!.RecipeId)
            .ToDictionary(g => g.Key, g => g.First().Id);
    }
}
