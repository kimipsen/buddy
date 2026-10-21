using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Proxies the recipe's small photo: the browser can't call Mealie (no CORS, and the token stays on
// the server), and MealieClient only ever passes an image/* body through.
public static class GetMealieRecipeImageHandler
{
    public static async Task<MealieOutcome<MealieImage>> Handle(
        GetMealieRecipeImage query,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<MealieImage>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(query.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, MealieImage>();
        }

        var image = await mealie.GetRecipeImageAsync(connection.Endpoint, query.RecipeId, cancellationToken);

        return image is MealieResponse<MealieImage>.Ok(var value)
            ? new MealieOutcome<MealieImage>.Success(value)
            : MealieOutcome.FromFailure<MealieImage>(image.FailureOf());
    }
}
