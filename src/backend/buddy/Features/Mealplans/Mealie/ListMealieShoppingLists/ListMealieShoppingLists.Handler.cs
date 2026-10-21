using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListMealieShoppingListsHandler
{
    public static async Task<MealieOutcome<IReadOnlyList<MealieShoppingList>>> Handle(
        ListMealieShoppingLists query,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<IReadOnlyList<MealieShoppingList>>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(query.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, IReadOnlyList<MealieShoppingList>>();
        }

        var lists = await mealie.ListShoppingListsAsync(connection.Endpoint, cancellationToken);

        return lists is MealieResponse<IReadOnlyList<MealieShoppingListDto>>.Ok(var items)
            ? new MealieOutcome<IReadOnlyList<MealieShoppingList>>.Success([.. items.Select(l => new MealieShoppingList(l.Id, l.Name ?? ""))])
            : new MealieOutcome<IReadOnlyList<MealieShoppingList>>.UpstreamFailed(lists.FailureOf());
    }
}
