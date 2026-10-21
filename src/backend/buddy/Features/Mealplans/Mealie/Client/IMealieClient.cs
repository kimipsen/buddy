namespace buddy.Features.Mealplans;

// Where and as whom to call Mealie. Built by a handler from the decrypted connection; never logged.
public sealed record MealieEndpoint(string BaseUrl, string Token);

public enum MealieFailureKind
{
    // Timeout, DNS or TLS failure, refused connection, a blocked address, a redirect.
    Unreachable,
    // Mealie answered 401/403: the token was revoked, or its user changed password.
    TokenRejected,
    // Mealie answered 404 for the thing asked for (a recipe deleted in Mealie, a list removed).
    NotFound,
    // Any other non-2xx, or a body Buddy can't parse.
    Error
}

public sealed record MealieFailure(MealieFailureKind Kind, string Message);

// A Mealie call either returns its value or says why not. Failures are expected (a home server
// that's off, a revoked token), so they're values rather than exceptions.
public union MealieResponse<T>(MealieResponse<T>.Ok, MealieResponse<T>.Failed)
{
    public sealed record Ok(T Value);
    public sealed record Failed(MealieFailure Failure);
}

public static class MealieResponseExtensions
{
    // The failure of a response already known not to be Ok.
    public static MealieFailure FailureOf<T>(this MealieResponse<T> response) => response switch
    {
        MealieResponse<T>.Failed(var failure) => failure,
        MealieResponse<T>.Ok => throw new InvalidOperationException("FailureOf called with a successful Mealie response."),
    };
}

// The 8 Mealie calls Buddy makes -- see docs/backend/analysis/mealie-integration.md, Question 8.
// Integration tests replace it with FakeMealieClient; MealieContractTests runs the real one
// against a pinned Mealie image.
public interface IMealieClient
{
    Task<MealieResponse<MealieAboutDto>> GetAboutAsync(MealieEndpoint endpoint, CancellationToken cancellationToken);

    Task<MealieResponse<MealieUserDto>> GetSelfAsync(MealieEndpoint endpoint, CancellationToken cancellationToken);

    Task<MealieResponse<MealiePageDto<MealieRecipeSummaryDto>>> SearchRecipesAsync(MealieEndpoint endpoint, string search, int page, int perPage, CancellationToken cancellationToken);

    // Mealie's /api/recipes/{slug} also accepts the recipe id.
    Task<MealieResponse<MealieRecipeDto>> GetRecipeAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken);

    Task<MealieResponse<MealieImage>> GetRecipeImageAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken);

    // Every entry from..to inclusive, all pages.
    Task<MealieResponse<IReadOnlyList<MealiePlanEntryDto>>> ListMealPlanAsync(MealieEndpoint endpoint, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<MealieResponse<IReadOnlyList<MealieShoppingListDto>>> ListShoppingListsAsync(MealieEndpoint endpoint, CancellationToken cancellationToken);

    Task<MealieResponse<Common.Unit>> AddRecipesToShoppingListAsync(
        MealieEndpoint endpoint, Guid listId, IReadOnlyList<MealieShoppingListRecipeDto> recipes, CancellationToken cancellationToken);
}
