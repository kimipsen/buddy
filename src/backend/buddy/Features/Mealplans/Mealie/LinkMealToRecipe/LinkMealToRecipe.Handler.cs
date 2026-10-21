using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

using Microsoft.Extensions.Caching.Memory;

namespace buddy.Features.Mealplans;

// The recipe is fetched first, so a mistyped id is a 404 rather than a dead link, and the stored
// slug and name are Mealie's current ones. Relinking to the same recipe with the same name and slug
// appends nothing. See docs/backend/analysis/mealie-integration.md, Question 4.
public static class LinkMealToRecipeHandler
{
    public static async Task<MealieOutcome<Meal>> Handle(
        LinkMealToRecipe command,
        IValidator<LinkMealToRecipe> validator,
        IMealEventStore meals,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        IMemoryCache cache,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new MealieOutcome<Meal>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<Meal>(access);
        }

        if (await LoadFamilyMealAsync(command.ChildId, command.MealId, meals, guardians, cancellationToken) is not { } meal)
        {
            return new MealieOutcome<Meal>.NotFound();
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

        var link = new MealieRecipeRef(recipe.Id, recipe.Slug ?? "", recipe.Name ?? "");

        if (meal.Recipe == link)
        {
            return new MealieOutcome<Meal>.Success(meal);
        }

        await meals.AppendAsync(meal.Id, [new MealLinkedToRecipe(meal.Id, link, command.UserId, time.GetUtcNow())], cancellationToken);

        return new MealieOutcome<Meal>.Success(meal with { Recipe = link, LastModifiedBy = command.UserId });
    }

    // The same rule UpdateMealDetails uses: the meal must be in the family's library and not
    // archived, otherwise NotFound. Rehydrated from the stream, so the append is an expected-version
    // append (StreamVersionTracker).
    internal static async Task<Meal?> LoadFamilyMealAsync(
        UserId childId, MealId mealId, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var meal = Meal.Rehydrate(await meals.ReadAsync(mealId, cancellationToken));

        if (meal is null || meal.IsArchived)
        {
            return null;
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);

        return familyMealIds.Contains(mealId) ? meal : null;
    }
}

// Needs no Mealie call and no connection: a family that disconnected can still drop a link.
// Idempotent: unlinking a meal with no link appends nothing.
public static class UnlinkMealRecipeHandler
{
    public static async Task<Result<Unit>> Handle(
        UnlinkMealRecipe command,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (await LinkMealToRecipeHandler.LoadFamilyMealAsync(command.ChildId, command.MealId, meals, guardians, cancellationToken) is not { } meal)
        {
            return new Result<Unit>.NotFound();
        }

        if (meal.Recipe is not null)
        {
            await meals.AppendAsync(meal.Id, [new MealRecipeUnlinked(meal.Id, command.UserId, time.GetUtcNow())], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
