using System.Collections.Concurrent;

using buddy.Common;
using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// Stands in for the family's Mealie server in the integration tests (MealieTestHost registers it
// instead of the real IMealieClient). Every test makes its own FakeMealieServer under a unique base
// URL, so tests sharing the host never see each other's recipes or calls.
internal sealed class FakeMealieClient : IMealieClient
{
    public static readonly FakeMealieClient Instance = new();

    private readonly ConcurrentDictionary<string, FakeMealieServer> _servers = new(StringComparer.Ordinal);

    public FakeMealieServer NewServer(string? baseUrl = null)
    {
        var server = new FakeMealieServer(baseUrl ?? $"https://{Guid.NewGuid():N}.mealie.example");
        _servers[server.BaseUrl] = server;
        return server;
    }

    public Task<MealieResponse<MealieAboutDto>> GetAboutAsync(MealieEndpoint endpoint, CancellationToken cancellationToken) =>
        Call(endpoint, requireToken: false, server => new MealieAboutDto(server.Version));

    public Task<MealieResponse<MealieUserDto>> GetSelfAsync(MealieEndpoint endpoint, CancellationToken cancellationToken) =>
        Call(endpoint, requireToken: true, server => new MealieUserDto(
            Guid.Parse("5b2f5781-e0d8-448e-94d0-c3f0ead4b2aa"), server.UserName, server.Admin, "Holm", server.HouseholdId, server.GroupId, "holm"));

    public Task<MealieResponse<MealiePageDto<MealieRecipeSummaryDto>>> SearchRecipesAsync(
        MealieEndpoint endpoint, string search, int page, int perPage, CancellationToken cancellationToken) =>
        Call(endpoint, requireToken: true, server =>
        {
            server.Searches.Add(search);
            var matching = server.Recipes.Values
                .Where(r => search.Length == 0 || (r.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();
            var items = matching.Skip((page - 1) * perPage).Take(perPage)
                .Select(r => new MealieRecipeSummaryDto(r.Id, r.Slug, r.Name, r.Description, r.Image, r.TotalTime, r.RecipeYield))
                .ToList();

            return new MealiePageDto<MealieRecipeSummaryDto>(page, perPage, matching.Count, Math.Max(1, (matching.Count + perPage - 1) / perPage), items);
        });

    public Task<MealieResponse<MealieRecipeDto>> GetRecipeAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken)
    {
        if (Find(endpoint) is { } server)
        {
            server.RecipeFetches++;
        }

        return CallOrNotFound(endpoint, server => server.Recipes.GetValueOrDefault(recipeId));
    }

    public Task<MealieResponse<MealieImage>> GetRecipeImageAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken) =>
        CallOrNotFound(endpoint, server => server.Recipes.TryGetValue(recipeId, out var recipe) && recipe.Image is not null
            ? new MealieImage([0x52, 0x49, 0x46, 0x46], "image/webp")
            : null);

    public Task<MealieResponse<IReadOnlyList<MealiePlanEntryDto>>> ListMealPlanAsync(
        MealieEndpoint endpoint, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Call(endpoint, requireToken: true, server => (IReadOnlyList<MealiePlanEntryDto>)[.. server.Plan.Where(e => e.Date >= from && e.Date <= to)]);

    public Task<MealieResponse<IReadOnlyList<MealieShoppingListDto>>> ListShoppingListsAsync(MealieEndpoint endpoint, CancellationToken cancellationToken) =>
        Call(endpoint, requireToken: true, server => (IReadOnlyList<MealieShoppingListDto>)[.. server.ShoppingLists]);

    public Task<MealieResponse<Unit>> AddRecipesToShoppingListAsync(
        MealieEndpoint endpoint, Guid listId, IReadOnlyList<MealieShoppingListRecipeDto> recipes, CancellationToken cancellationToken)
    {
        if (Check(endpoint, requireToken: true) is { } failure)
        {
            return Task.FromResult<MealieResponse<Unit>>(new MealieResponse<Unit>.Failed(failure));
        }

        var server = Find(endpoint)!;

        // Like the real Mealie (MealieContractTests): an unknown list is a 500, not a 404.
        if (server.ShoppingLists.All(l => l.Id != listId))
        {
            return Task.FromResult<MealieResponse<Unit>>(new MealieResponse<Unit>.Failed(new MealieFailure(MealieFailureKind.Error, "Mealie answered with status 500.")));
        }

        server.ShoppingCalls.Add((listId, recipes));
        return Task.FromResult<MealieResponse<Unit>>(new MealieResponse<Unit>.Ok(Unit.Value));
    }

    private FakeMealieServer? Find(MealieEndpoint endpoint) => _servers.GetValueOrDefault(endpoint.BaseUrl);

    private Task<MealieResponse<T>> Call<T>(MealieEndpoint endpoint, bool requireToken, Func<FakeMealieServer, T> answer) =>
        Task.FromResult<MealieResponse<T>>(Check(endpoint, requireToken) is { } failure
            ? new MealieResponse<T>.Failed(failure)
            : new MealieResponse<T>.Ok(answer(Find(endpoint)!)));

    private Task<MealieResponse<T>> CallOrNotFound<T>(MealieEndpoint endpoint, Func<FakeMealieServer, T?> answer)
    {
        if (Check(endpoint, requireToken: true) is { } failure)
        {
            return Task.FromResult<MealieResponse<T>>(new MealieResponse<T>.Failed(failure));
        }

        return Task.FromResult<MealieResponse<T>>(answer(Find(endpoint)!) is { } value
            ? new MealieResponse<T>.Ok(value)
            : new MealieResponse<T>.Failed(new MealieFailure(MealieFailureKind.NotFound, "Mealie couldn't find it.")));
    }

    private MealieFailure? Check(MealieEndpoint endpoint, bool requireToken)
    {
        if (Find(endpoint) is not { } server || server.Down)
        {
            return new MealieFailure(MealieFailureKind.Unreachable, "Buddy couldn't reach your Mealie.");
        }

        if (server.FailWith is { } kind)
        {
            return new MealieFailure(kind, "Mealie answered with status 500.");
        }

        if (requireToken && !server.ValidTokens.Contains(endpoint.Token))
        {
            return new MealieFailure(MealieFailureKind.TokenRejected, "Your Mealie rejected Buddy's API token.");
        }

        return null;
    }
}

internal sealed class FakeMealieServer(string baseUrl)
{
    public string BaseUrl { get; } = baseUrl;

    public string Version { get; set; } = "v3.29.0";

    public string UserName { get; set; } = "buddy-sync";

    public bool Admin { get; set; }

    public bool Down { get; set; }

    // Every call (after the reachability check) fails with this kind.
    public MealieFailureKind? FailWith { get; set; }

    public Guid HouseholdId { get; } = Guid.CreateVersion7();

    public Guid GroupId { get; } = Guid.CreateVersion7();

    public HashSet<string> ValidTokens { get; } = ["mealie-token-1234"];

    public ConcurrentDictionary<Guid, MealieRecipeDto> Recipes { get; } = new();

    public List<MealiePlanEntryDto> Plan { get; } = [];

    public List<MealieShoppingListDto> ShoppingLists { get; } = [new(Guid.CreateVersion7(), "Weekly shop")];

    public List<string> Searches { get; } = [];

    public List<(Guid ListId, IReadOnlyList<MealieShoppingListRecipeDto> Recipes)> ShoppingCalls { get; } = [];

    public int RecipeFetches { get; set; }

    public MealieRecipeDto AddRecipe(string name, string? image = "abc", string? description = "")
    {
        var id = Guid.CreateVersion7();
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        var recipe = new MealieRecipeDto(
            id, slug, name, description, image, "1 hour 30 minutes", "6 servings", "https://recipes.example/" + slug,
            [new MealieIngredientDto(null, "500 g minced beef", null), new MealieIngredientDto("For the sauce", null, null)],
            [new MealieStepDto(null, "Brown the beef."), new MealieStepDto(null, "")]);
        Recipes[id] = recipe;
        return recipe;
    }

    public void AddPlanEntry(DateOnly date, string entryType, MealieRecipeDto? recipe = null, string? title = null, string? text = null) =>
        Plan.Add(new MealiePlanEntryDto(
            Plan.Count + 1,
            date,
            entryType,
            title ?? "",
            text ?? "",
            recipe?.Id,
            recipe is null ? null : new MealieRecipeSummaryDto(recipe.Id, recipe.Slug, recipe.Name, recipe.Description, recipe.Image, recipe.TotalTime, recipe.RecipeYield)));
}
