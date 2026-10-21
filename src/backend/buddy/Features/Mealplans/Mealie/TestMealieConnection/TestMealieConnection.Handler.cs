using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Re-runs the connect-time live check with the stored token, and counts what the settings card
// shows ("214 recipes, 2 shopping lists"). A failing Mealie is a Failed result, not an error.
public static class TestMealieConnectionHandler
{
    public static async Task<MealieOutcome<TestMealieConnectionResult>> Handle(
        TestMealieConnection command,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<TestMealieConnectionResult>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(command.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, TestMealieConnectionResult>();
        }

        var (passed, rejected) = await MealieLiveCheck.RunAsync(mealie, connection.Endpoint, cancellationToken);

        if (passed is null)
        {
            return Failed(rejected?.Failure ?? new MealieFailure(MealieFailureKind.Error, ""));
        }

        if (await mealie.SearchRecipesAsync(connection.Endpoint, "", 1, 1, cancellationToken) is not MealieResponse<MealiePageDto<MealieRecipeSummaryDto>>.Ok(var recipes))
        {
            return Failed(new MealieFailure(MealieFailureKind.Error, "Mealie answered, but its recipes couldn't be read."));
        }

        if (await mealie.ListShoppingListsAsync(connection.Endpoint, cancellationToken) is not MealieResponse<IReadOnlyList<MealieShoppingListDto>>.Ok(var lists))
        {
            return Failed(new MealieFailure(MealieFailureKind.Error, "Mealie answered, but its shopping lists couldn't be read."));
        }

        return new MealieOutcome<TestMealieConnectionResult>.Success(new TestMealieConnectionResult.Succeeded(passed.Version, recipes.Total, lists.Count));
    }

    private static MealieOutcome<TestMealieConnectionResult> Failed(MealieFailure failure) =>
        new MealieOutcome<TestMealieConnectionResult>.Success(new TestMealieConnectionResult.Failed(MealieErrorCodes.For(failure.Kind), failure.Message));
}
