using System.Collections.Immutable;

using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

// One bulk call: every linked recipe planned in the range, with how many times it's planned as the
// quantity. Mealie merges the ingredients. Buddy writes no event -- nothing in Buddy changes -- and
// a double click is stopped by the Idempotency-Key, since Mealie adds quantities on every call.
// See docs/backend/analysis/mealie-integration.md, Question 6.
public static class AddMealPlanToMealieShoppingListHandler
{
    public static async Task<MealieOutcome<AddedToShoppingList>> Handle(
        AddMealPlanToMealieShoppingList command,
        IValidator<AddMealPlanToMealieShoppingList> validator,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        ILogger<AddMealPlanToMealieShoppingList> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new MealieOutcome<AddedToShoppingList>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<AddedToShoppingList>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(command.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, AddedToShoppingList>();
        }

        var planId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);
        var plan = planId is null ? null : await mealPlans.FindSnapshotAsync(planId, cancellationToken);
        var slots = command.Slots.ToHashSet();

        var planned = (plan?.Assignments ?? ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>.Empty)
            .Where(a => a.Key.Date >= command.From && a.Key.Date <= command.To && slots.Contains(a.Key.Slot))
            .OrderBy(a => a.Key.Date)
            .ThenBy(a => a.Key.Slot)
            .Select(a => a.Value.MealId)
            .ToList();

        Dictionary<MealId, Meal?> mealsById = [];

        foreach (var mealId in planned.Distinct())
        {
            mealsById[mealId] = await meals.FindSnapshotAsync(mealId, cancellationToken);
        }

        var eatingOut = planned.Count(id => mealsById[id] is { EatingOut: not MealEatingOut.None });
        var cooked = planned.Select(id => mealsById[id]).OfType<Meal>().Where(m => m.EatingOut == MealEatingOut.None).ToList();

        List<string> unlinked = [.. cooked.Where(m => m.Recipe is null).Select(m => m.Name).Distinct(StringComparer.Ordinal)];
        List<AddedRecipe> added =
        [
            .. cooked
                .Where(m => m.Recipe is not null)
                .GroupBy(m => m.Recipe!.RecipeId)
                .Select(g => new AddedRecipe(g.Key, g.First().Recipe!.Name, g.Count()))
        ];

        if (added.Count > 0)
        {
            var sent = await mealie.AddRecipesToShoppingListAsync(
                connection.Endpoint,
                command.ListId,
                [.. added.Select(r => new MealieShoppingListRecipeDto(r.RecipeId, r.Times))],
                cancellationToken);

            if (sent is MealieResponse<Common.Unit>.Failed(var failure))
            {
                logger.MealieCallFailed(nameof(AddMealPlanToMealieShoppingList), command.ChildId.Value, failure.Kind);

                // Mealie answers an unknown list with a 500 (seen in MealieContractTests), so on an
                // error ask whether the list is still there before blaming the server.
                return new MealieOutcome<AddedToShoppingList>.UpstreamFailed(
                    failure.Kind is MealieFailureKind.NotFound or MealieFailureKind.Error
                    && !await ListExistsAsync(mealie, connection.Endpoint, command.ListId, cancellationToken)
                        ? new MealieFailure(MealieFailureKind.Error, "That shopping list no longer exists in Mealie. Pick another one.")
                        : failure);
            }
        }

        return new MealieOutcome<AddedToShoppingList>.Success(new AddedToShoppingList(added, unlinked, eatingOut));
    }

    // True when the lists can't be read either: then the server, not the list, is the problem.
    private static async Task<bool> ListExistsAsync(IMealieClient mealie, MealieEndpoint endpoint, Guid listId, CancellationToken cancellationToken) =>
        await mealie.ListShoppingListsAsync(endpoint, cancellationToken) is not MealieResponse<IReadOnlyList<MealieShoppingListDto>>.Ok(var lists)
        || lists.Any(l => l.Id == listId);
}
